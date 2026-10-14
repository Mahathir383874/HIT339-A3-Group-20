using Microsoft.EntityFrameworkCore;
using Bibliotheca.Data;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.Enums;

namespace Bibliotheca.Services
{
    public enum TransferError
    {
        None,
        ItemNotFound,
        TransferNotFound,
        ItemHasNoBranch,
        DestinationInvalid,
        SameBranch,
        ItemNotTransferable,
        ActiveTransferExists,
        ItemOnHoldShelf,
        InvalidStatus,
        LocationChanged
    }

    public sealed class TransferResult
    {
        public TransferError Error { get; init; }
        public bool Succeeded => Error == TransferError.None;
        public string Message { get; init; } = string.Empty;
        public BranchTransfer? Transfer { get; init; }

        internal static TransferResult Fail(TransferError error, string message)
            => new() { Error = error, Message = message };
    }

    /// <summary>
    /// Inter-branch transfers: Requested -> InTransit -> Completed (or Cancelled from either of the first two).
    /// Every step is validated and recorded with who did it and when. Completing a transfer is the only
    /// thing that moves an item to another branch, and the transfer row is kept as history.
    /// </summary>
    public interface ITransferService
    {
        Task<TransferResult> RequestAsync(int itemId, int toBranchId, string? notes, string? actor);

        Task<TransferResult> DispatchAsync(int transferId, string? actor);

        Task<TransferResult> CompleteAsync(int transferId, string? actor);

        Task<TransferResult> CancelAsync(int transferId, string? reason, string? actor);

        Task<bool> HasActiveTransferAsync(int itemId);
    }

    public class TransferService : ITransferService
    {
        private readonly ApplicationDbContext _context;
        private readonly IReservationService _reservations;

        public TransferService(ApplicationDbContext context, IReservationService reservations)
        {
            _context = context;
            _reservations = reservations;
        }

        public Task<bool> HasActiveTransferAsync(int itemId) =>
            _context.BranchTransfers.AnyAsync(t =>
                t.ItemId == itemId && (t.Status == TransferStatus.Requested || t.Status == TransferStatus.InTransit));

        // =====================================================================
        // Request
        // =====================================================================
        public async Task<TransferResult> RequestAsync(int itemId, int toBranchId, string? notes, string? actor)
        {
            var item = await _context.Items.Include(i => i.Branch).FirstOrDefaultAsync(i => i.Id == itemId);
            if (item == null)
            {
                return TransferResult.Fail(TransferError.ItemNotFound, "The selected item could not be found.");
            }

            if (!item.BranchId.HasValue || item.Branch == null)
            {
                return TransferResult.Fail(TransferError.ItemHasNoBranch,
                    $"'{item.Name}' is not assigned to a branch yet. Edit the item and choose its branch first.");
            }

            var destination = await _context.Branches.FirstOrDefaultAsync(b => b.Id == toBranchId);
            if (destination == null || !destination.IsActive)
            {
                return TransferResult.Fail(TransferError.DestinationInvalid, "Please choose an active destination branch.");
            }

            if (destination.Id == item.BranchId.Value)
            {
                return TransferResult.Fail(TransferError.SameBranch,
                    $"'{item.Name}' is already at {destination.Name}. The source and destination branch must be different.");
            }

            if (item.Status == ItemStatus.Borrowed)
            {
                return TransferResult.Fail(TransferError.ItemNotTransferable,
                    $"'{item.Name}' is on loan. It can be transferred after it has been returned.");
            }

            if (item.Status == ItemStatus.Destroyed)
            {
                return TransferResult.Fail(TransferError.ItemNotTransferable,
                    $"'{item.Name}' has been destroyed and cannot be transferred.");
            }

            if (await HasActiveTransferAsync(item.Id))
            {
                return TransferResult.Fail(TransferError.ActiveTransferExists,
                    $"'{item.Name}' already has a transfer in progress. Complete or cancel it before requesting another.");
            }

            var onHoldShelf = await _context.Reservations.AnyAsync(r => r.ItemId == item.Id && r.Status == ReservationStatus.Ready);
            if (onHoldShelf)
            {
                return TransferResult.Fail(TransferError.ItemOnHoldShelf,
                    $"'{item.Name}' is on the hold shelf for a patron. Cancel or fulfil that hold before transferring the item.");
            }

            var transfer = new BranchTransfer
            {
                ItemId = item.Id,
                FromBranchId = item.BranchId.Value,
                ToBranchId = destination.Id,
                Status = TransferStatus.Requested,
                RequestedDate = DateTime.UtcNow,
                RequestedBy = Clean(actor, 100),
                Notes = Clean(notes, 250)
            };

            _context.BranchTransfers.Add(transfer);
            await _context.SaveChangesAsync();

            return new TransferResult
            {
                Error = TransferError.None,
                Message = $"Transfer #{transfer.Id} requested: '{item.Name}' from {item.Branch.Name} to {destination.Name}.",
                Transfer = transfer
            };
        }

        // =====================================================================
        // Dispatch (item leaves the source branch)
        // =====================================================================
        public async Task<TransferResult> DispatchAsync(int transferId, string? actor)
        {
            var transfer = await LoadAsync(transferId);
            if (transfer == null)
            {
                return TransferResult.Fail(TransferError.TransferNotFound, "That transfer could not be found.");
            }

            if (transfer.Status != TransferStatus.Requested)
            {
                return TransferResult.Fail(TransferError.InvalidStatus,
                    $"Only a requested transfer can be dispatched (this one is {Describe(transfer.Status)}).");
            }

            var problem = CheckItemStillMovable(transfer);
            if (problem != null) return problem;

            transfer.Status = TransferStatus.InTransit;
            transfer.DispatchedDate = DateTime.UtcNow;
            transfer.DispatchedBy = Clean(actor, 100);
            await _context.SaveChangesAsync();

            return new TransferResult
            {
                Error = TransferError.None,
                Message = $"Transfer #{transfer.Id} dispatched: '{transfer.Item!.Name}' is now in transit to {transfer.ToBranch?.Name}.",
                Transfer = transfer
            };
        }

        // =====================================================================
        // Complete (item arrives; its branch changes)
        // =====================================================================
        public async Task<TransferResult> CompleteAsync(int transferId, string? actor)
        {
            var transfer = await LoadAsync(transferId);
            if (transfer == null)
            {
                return TransferResult.Fail(TransferError.TransferNotFound, "That transfer could not be found.");
            }

            if (transfer.Status != TransferStatus.InTransit)
            {
                return TransferResult.Fail(TransferError.InvalidStatus,
                    transfer.Status == TransferStatus.Requested
                        ? "This transfer has not been dispatched yet. Dispatch it before receiving it."
                        : $"Only a transfer that is in transit can be received (this one is {Describe(transfer.Status)}).");
            }

            var problem = CheckItemStillMovable(transfer);
            if (problem != null) return problem;

            var item = transfer.Item!;
            item.BranchId = transfer.ToBranchId;

            transfer.Status = TransferStatus.Completed;
            transfer.CompletedDate = DateTime.UtcNow;
            transfer.CompletedBy = Clean(actor, 100);
            await _context.SaveChangesAsync();

            // The item has arrived: patrons who were waiting for it (queue paused during the transfer) can now be served.
            var promoted = await _reservations.AdvanceQueueAsync(item.Id);

            var message = $"Transfer #{transfer.Id} completed: '{item.Name}' is now held at {transfer.ToBranch?.Name}.";
            if (promoted?.Borrower != null)
            {
                message += $" {promoted.Borrower.FullName} has been notified that it is ready for pickup.";
            }

            return new TransferResult { Error = TransferError.None, Message = message, Transfer = transfer };
        }

        // =====================================================================
        // Cancel
        // =====================================================================
        public async Task<TransferResult> CancelAsync(int transferId, string? reason, string? actor)
        {
            var transfer = await LoadAsync(transferId);
            if (transfer == null)
            {
                return TransferResult.Fail(TransferError.TransferNotFound, "That transfer could not be found.");
            }

            if (transfer.Status != TransferStatus.Requested && transfer.Status != TransferStatus.InTransit)
            {
                return TransferResult.Fail(TransferError.InvalidStatus,
                    $"Only a requested or in-transit transfer can be cancelled (this one is {Describe(transfer.Status)}).");
            }

            transfer.Status = TransferStatus.Cancelled;
            transfer.CancelledDate = DateTime.UtcNow;
            transfer.CancelledBy = Clean(actor, 100);
            transfer.CancelReason = Clean(reason, 250);
            await _context.SaveChangesAsync();

            // The item stays where it was and can be lent again, so a paused hold queue resumes.
            await _reservations.AdvanceQueueAsync(transfer.ItemId);

            return new TransferResult
            {
                Error = TransferError.None,
                Message = $"Transfer #{transfer.Id} cancelled. '{transfer.Item?.Name}' stays at {transfer.FromBranch?.Name}.",
                Transfer = transfer
            };
        }

        // =====================================================================
        // helpers
        // =====================================================================
        private Task<BranchTransfer?> LoadAsync(int transferId) =>
            _context.BranchTransfers
                .Include(t => t.Item)
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .FirstOrDefaultAsync(t => t.Id == transferId);

        /// <summary>Re-checks the item before it leaves / arrives so a stale transfer can never corrupt its location.</summary>
        private static TransferResult? CheckItemStillMovable(BranchTransfer transfer)
        {
            var item = transfer.Item;
            if (item == null)
            {
                return TransferResult.Fail(TransferError.ItemNotFound, "The item for this transfer no longer exists. Cancel the transfer.");
            }

            if (item.BranchId != transfer.FromBranchId)
            {
                return TransferResult.Fail(TransferError.LocationChanged,
                    $"'{item.Name}' is no longer recorded at {transfer.FromBranch?.Name}, so this transfer is out of date. Cancel it and request a new one.");
            }

            if (item.Status == ItemStatus.Borrowed || item.Status == ItemStatus.Destroyed)
            {
                return TransferResult.Fail(TransferError.ItemNotTransferable,
                    $"'{item.Name}' is now {item.Status.ToString().ToLowerInvariant()}, so it cannot be moved. Cancel the transfer.");
            }

            return null;
        }

        private static string Describe(TransferStatus status) => status switch
        {
            TransferStatus.InTransit => "in transit",
            _ => status.ToString().ToLowerInvariant()
        };

        private static string? Clean(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var text = value.Trim();
            return text.Length <= max ? text : text.Substring(0, max);
        }
    }
}

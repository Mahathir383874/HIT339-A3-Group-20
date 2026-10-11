using Microsoft.EntityFrameworkCore;
using Bibliotheca.Data;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.Enums;
using Bibliotheca.Models.ViewModels;

namespace Bibliotheca.Services
{
    public enum ReservationError
    {
        None,
        CardNotFound,
        BorrowerInactive,
        ItemNotFound,
        ItemNotReservable,
        ItemBorrowable,
        AlreadyReserved,
        AlreadyOnLoan,
        ReservationNotFound,
        NotOwner,
        NotActive
    }

    public sealed class ReservationResult
    {
        public ReservationError Error { get; init; }
        public bool Succeeded => Error == ReservationError.None;
        public string Message { get; init; } = string.Empty;
        public Reservation? Reservation { get; init; }
        public int? QueuePosition { get; init; }

        internal static ReservationResult Fail(ReservationError error, string message)
            => new() { Error = error, Message = message };
    }

    /// <summary>
    /// Reservation (hold) and waitlist rules, shared by the self-service kiosk and the reception desk.
    /// Queue order is first-come-first-served and positions are always calculated from the active holds.
    /// </summary>
    public interface IReservationService
    {
        /// <summary>Places a hold for the borrower with this card on the item with this library code.</summary>
        Task<ReservationResult> PlaceHoldAsync(string? cardNumber, string? libraryCode, string placedVia);

        /// <summary>
        /// Cancels an active hold. When <paramref name="onlyForBorrowerId"/> is given the hold must belong to that
        /// borrower (the kiosk passes the signed-in patron). Everyone behind the cancelled hold moves up.
        /// </summary>
        Task<ReservationResult> CancelAsync(int reservationId, int? onlyForBorrowerId, string reason);

        /// <summary>
        /// Re-checks the hold queue of an item and promotes the next waiting patron to Ready when the item
        /// can be lent. Safe to call at any time: it does nothing while the item is out, damaged or being
        /// transferred, and cancels the holds if the item has been destroyed.
        /// Returns the hold that became Ready (borrower loaded), or null.
        /// </summary>
        Task<Reservation?> AdvanceQueueAsync(int itemId);

        /// <summary>Cancels every active hold on an item (for example when it is destroyed).</summary>
        Task<int> CancelAllActiveForItemAsync(int itemId, string reason);

        Task<List<ReservationRow>> GetRowsAsync(string? filter, string? searchTerm);

        Task<List<ReservationRow>> GetActiveForBorrowerAsync(int borrowerId);

        Task<List<ReservationRow>> GetQueueForItemAsync(int itemId);

        Task<int> GetQueueLengthAsync(int itemId);

        Task<Dictionary<int, ItemLendingState>> GetLendingStatesAsync(IEnumerable<int> itemIds);
    }

    public class ReservationService : IReservationService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notifications;

        public ReservationService(ApplicationDbContext context, INotificationService notifications)
        {
            _context = context;
            _notifications = notifications;
        }

        // =====================================================================
        // Place a hold
        // =====================================================================
        public async Task<ReservationResult> PlaceHoldAsync(string? cardNumber, string? libraryCode, string placedVia)
        {
            var card = CirculationService.Normalise(cardNumber);
            var code = CirculationService.Normalise(libraryCode);

            if (card.Length == 0)
            {
                return ReservationResult.Fail(ReservationError.CardNotFound, "Please enter the borrower's library card number.");
            }

            var borrower = await _context.Borrowers.FirstOrDefaultAsync(b => b.CardNumber.ToUpper() == card);
            if (borrower == null)
            {
                return ReservationResult.Fail(ReservationError.CardNotFound, $"No borrower found with card number '{cardNumber?.Trim()}'.");
            }

            if (!borrower.IsActive)
            {
                return ReservationResult.Fail(ReservationError.BorrowerInactive,
                    $"Borrower account '{borrower.FullName}' is inactive/suspended, so holds cannot be placed.");
            }

            if (code.Length == 0)
            {
                return ReservationResult.Fail(ReservationError.ItemNotFound, "Please enter the item's library code.");
            }

            var item = await _context.Items.Include(i => i.Branch).FirstOrDefaultAsync(i => i.LibraryCode.ToUpper() == code);
            if (item == null)
            {
                return ReservationResult.Fail(ReservationError.ItemNotFound, $"No library item found with code '{libraryCode?.Trim()}'.");
            }

            if (item.Status == ItemStatus.Destroyed)
            {
                return ReservationResult.Fail(ReservationError.ItemNotReservable,
                    $"'{item.Name}' has been destroyed and can no longer be reserved.");
            }

            // Active holds on this item, oldest first
            var queue = await _context.Reservations
                .Where(r => r.ItemId == item.Id && (r.Status == ReservationStatus.Waiting || r.Status == ReservationStatus.Ready))
                .OrderBy(r => r.Id)
                .ToListAsync();

            // No duplicate holds
            var existing = queue.FirstOrDefault(r => r.BorrowerId == borrower.Id);
            if (existing != null)
            {
                var position = queue.FindIndex(r => r.Id == existing.Id) + 1;
                var detail = existing.Status == ReservationStatus.Ready
                    ? "It is ready for pickup."
                    : $"You are number {position} in the queue.";
                return ReservationResult.Fail(ReservationError.AlreadyReserved,
                    $"{borrower.FirstName} already has a hold on '{item.Name}'. {detail}");
            }

            // A patron cannot hold an item they already have on loan
            var alreadyHasIt = await _context.Loans.AnyAsync(l =>
                l.ItemId == item.Id && l.BorrowerId == borrower.Id && l.Status != LoanStatus.Returned);
            if (alreadyHasIt)
            {
                return ReservationResult.Fail(ReservationError.AlreadyOnLoan,
                    $"{borrower.FirstName} already has '{item.Name}' on loan.");
            }

            // Holds are for items that cannot be borrowed right now
            var inTransfer = await HasActiveTransferAsync(item.Id);
            if (item.Status == ItemStatus.Available && !inTransfer && queue.Count == 0)
            {
                return ReservationResult.Fail(ReservationError.ItemBorrowable,
                    $"'{item.Name}' is available right now, so it can be borrowed instead of reserved.");
            }

            var now = DateTime.UtcNow;
            var reservation = new Reservation
            {
                ItemId = item.Id,
                BorrowerId = borrower.Id,
                Status = ReservationStatus.Waiting,
                CreatedDate = now,
                PlacedVia = string.IsNullOrWhiteSpace(placedVia) ? null : placedVia.Trim()
            };

            var queuePosition = queue.Count + 1;
            _context.Reservations.Add(reservation);
            _notifications.StageReservationPlaced(borrower, item, reservation, queuePosition);
            await _context.SaveChangesAsync();

            // Self-heal: if the item is actually free (for example a transfer just finished) the head of the queue is promoted.
            await AdvanceQueueAsync(item.Id);

            var message = reservation.Status == ReservationStatus.Ready
                ? $"Hold placed on '{item.Name}'. It is ready for pickup."
                : queuePosition == 1
                    ? $"Hold placed on '{item.Name}'. You are next in line."
                    : $"Hold placed on '{item.Name}'. You are number {queuePosition} in the queue.";

            return new ReservationResult
            {
                Error = ReservationError.None,
                Message = message,
                Reservation = reservation,
                QueuePosition = queuePosition
            };
        }

        // =====================================================================
        // Cancel a hold
        // =====================================================================
        public async Task<ReservationResult> CancelAsync(int reservationId, int? onlyForBorrowerId, string reason)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Item)
                .Include(r => r.Borrower)
                .FirstOrDefaultAsync(r => r.Id == reservationId);

            if (reservation == null)
            {
                return ReservationResult.Fail(ReservationError.ReservationNotFound, "That reservation could not be found.");
            }

            if (onlyForBorrowerId.HasValue && reservation.BorrowerId != onlyForBorrowerId.Value)
            {
                return ReservationResult.Fail(ReservationError.NotOwner, "That reservation does not belong to this account.");
            }

            if (reservation.Status != ReservationStatus.Waiting && reservation.Status != ReservationStatus.Ready)
            {
                return ReservationResult.Fail(ReservationError.NotActive, "This reservation is no longer active.");
            }

            reservation.Status = ReservationStatus.Cancelled;
            reservation.CancelledDate = DateTime.UtcNow;
            reservation.CancelReason = Truncate(reason, 200);
            await _context.SaveChangesAsync();

            // Patrons behind this hold move up automatically (positions are calculated from the active holds).
            // If the cancelled hold was the one waiting on the shelf, the next patron is promoted now.
            var promoted = await AdvanceQueueAsync(reservation.ItemId);

            var itemName = reservation.Item?.Name ?? "the item";
            var message = $"Reservation for '{itemName}' cancelled.";
            if (promoted?.Borrower != null)
            {
                message += $" {promoted.Borrower.FullName} is next in line and has been notified that the item is ready.";
            }

            return new ReservationResult
            {
                Error = ReservationError.None,
                Message = message,
                Reservation = reservation
            };
        }

        // =====================================================================
        // Queue advancement
        // =====================================================================
        public async Task<Reservation?> AdvanceQueueAsync(int itemId)
        {
            var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == itemId);
            if (item == null) return null;

            if (item.Status == ItemStatus.Destroyed)
            {
                await CancelAllActiveForItemAsync(itemId, "The item has been recorded as destroyed and can no longer be lent.");
                return null;
            }

            // Still out on loan or being repaired
            if (item.Status != ItemStatus.Available) return null;

            // Moving between branches: the queue resumes when the transfer is completed or cancelled
            if (await HasActiveTransferAsync(itemId)) return null;

            var queue = await _context.Reservations
                .Include(r => r.Borrower)
                .Where(r => r.ItemId == itemId && (r.Status == ReservationStatus.Waiting || r.Status == ReservationStatus.Ready))
                .OrderBy(r => r.Id)
                .ToListAsync();

            if (queue.Count == 0) return null;

            var holder = queue.FirstOrDefault(r => r.Status == ReservationStatus.Ready);
            if (holder != null)
            {
                // Someone is already waiting for it; just keep the pickup branch in step with where the item is.
                if (holder.PickupBranchId != item.BranchId)
                {
                    holder.PickupBranchId = item.BranchId;
                    await _context.SaveChangesAsync();
                }

                return null;
            }

            var now = DateTime.UtcNow;
            Reservation? promoted = null;

            foreach (var candidate in queue)
            {
                if (candidate.Borrower == null || !candidate.Borrower.IsActive)
                {
                    // A suspended membership cannot collect items: skip to the next patron.
                    candidate.Status = ReservationStatus.Cancelled;
                    candidate.CancelledDate = now;
                    candidate.CancelReason = "Library membership is not active.";
                    continue;
                }

                candidate.Status = ReservationStatus.Ready;
                candidate.ReadyDate = now;
                candidate.PickupBranchId = item.BranchId;

                var branchName = item.BranchId.HasValue
                    ? await _context.Branches.Where(b => b.Id == item.BranchId.Value).Select(b => b.Name).FirstOrDefaultAsync()
                    : null;

                _notifications.StageReservationReady(candidate.Borrower, item, candidate, branchName);
                promoted = candidate;
                break;
            }

            await _context.SaveChangesAsync();
            return promoted;
        }

        public async Task<int> CancelAllActiveForItemAsync(int itemId, string reason)
        {
            var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == itemId);
            var active = await _context.Reservations
                .Include(r => r.Borrower)
                .Where(r => r.ItemId == itemId && (r.Status == ReservationStatus.Waiting || r.Status == ReservationStatus.Ready))
                .ToListAsync();

            if (active.Count == 0) return 0;

            var now = DateTime.UtcNow;
            foreach (var reservation in active)
            {
                reservation.Status = ReservationStatus.Cancelled;
                reservation.CancelledDate = now;
                reservation.CancelReason = Truncate(reason, 200);

                if (item != null && reservation.Borrower != null)
                {
                    _notifications.StageReservationCancelled(reservation.Borrower, item, reservation, reason);
                }
            }

            await _context.SaveChangesAsync();
            return active.Count;
        }

        // =====================================================================
        // Queries
        // =====================================================================
        public async Task<List<ReservationRow>> GetRowsAsync(string? filter, string? searchTerm)
        {
            var all = await _context.Reservations
                .AsNoTracking()
                .Include(r => r.Item)
                    .ThenInclude(i => i!.Branch)
                .Include(r => r.Borrower)
                .Include(r => r.PickupBranch)
                .ToListAsync();

            var positions = BuildPositions(all.Where(IsActive));

            IEnumerable<Reservation> selected = all;
            if (string.Equals(filter, "History", StringComparison.OrdinalIgnoreCase))
            {
                selected = all.Where(r => !IsActive(r));
            }
            else if (!string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase))
            {
                selected = all.Where(IsActive);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLowerInvariant();
                selected = selected.Where(r =>
                    (r.Item?.Name ?? string.Empty).ToLowerInvariant().Contains(term) ||
                    (r.Item?.LibraryCode ?? string.Empty).ToLowerInvariant().Contains(term) ||
                    (r.Borrower?.FirstName ?? string.Empty).ToLowerInvariant().Contains(term) ||
                    (r.Borrower?.LastName ?? string.Empty).ToLowerInvariant().Contains(term) ||
                    (r.Borrower?.CardNumber ?? string.Empty).ToLowerInvariant().Contains(term));
            }

            // Active holds: grouped by item in queue order; history: newest first
            var ordered = string.Equals(filter, "History", StringComparison.OrdinalIgnoreCase)
                ? selected.OrderByDescending(r => r.Id)
                : selected.OrderByDescending(r => IsActive(r)).ThenBy(r => r.ItemId).ThenBy(r => r.Id);

            return ordered.Select(r => ToRow(r, positions)).ToList();
        }

        public async Task<List<ReservationRow>> GetActiveForBorrowerAsync(int borrowerId)
        {
            var mine = await _context.Reservations
                .AsNoTracking()
                .Include(r => r.Item)
                    .ThenInclude(i => i!.Branch)
                .Include(r => r.Borrower)
                .Include(r => r.PickupBranch)
                .Where(r => r.BorrowerId == borrowerId && (r.Status == ReservationStatus.Waiting || r.Status == ReservationStatus.Ready))
                .OrderBy(r => r.Id)
                .ToListAsync();

            if (mine.Count == 0) return new List<ReservationRow>();

            var positions = await PositionsForItemsAsync(mine.Select(r => r.ItemId).Distinct().ToList());
            return mine.Select(r => ToRow(r, positions)).ToList();
        }

        public async Task<List<ReservationRow>> GetQueueForItemAsync(int itemId)
        {
            var queue = await _context.Reservations
                .AsNoTracking()
                .Include(r => r.Item)
                    .ThenInclude(i => i!.Branch)
                .Include(r => r.Borrower)
                .Include(r => r.PickupBranch)
                .Where(r => r.ItemId == itemId && (r.Status == ReservationStatus.Waiting || r.Status == ReservationStatus.Ready))
                .OrderBy(r => r.Id)
                .ToListAsync();

            var positions = BuildPositions(queue);
            return queue.Select(r => ToRow(r, positions)).ToList();
        }

        public async Task<int> GetQueueLengthAsync(int itemId)
        {
            return await _context.Reservations.CountAsync(r =>
                r.ItemId == itemId && (r.Status == ReservationStatus.Waiting || r.Status == ReservationStatus.Ready));
        }

        public async Task<Dictionary<int, ItemLendingState>> GetLendingStatesAsync(IEnumerable<int> itemIds)
        {
            var ids = itemIds.Distinct().ToList();
            var states = new Dictionary<int, ItemLendingState>();
            if (ids.Count == 0) return states;

            var active = await _context.Reservations
                .AsNoTracking()
                .Where(r => ids.Contains(r.ItemId) && (r.Status == ReservationStatus.Waiting || r.Status == ReservationStatus.Ready))
                .OrderBy(r => r.Id)
                .Select(r => new { r.ItemId, r.BorrowerId, r.Status })
                .ToListAsync();

            var transfers = await _context.BranchTransfers
                .AsNoTracking()
                .Include(t => t.ToBranch)
                .Where(t => ids.Contains(t.ItemId) && (t.Status == TransferStatus.Requested || t.Status == TransferStatus.InTransit))
                .ToListAsync();

            foreach (var id in ids)
            {
                var queue = active.Where(a => a.ItemId == id).ToList();
                var transfer = transfers.FirstOrDefault(t => t.ItemId == id);

                states[id] = new ItemLendingState
                {
                    ItemId = id,
                    InTransfer = transfer != null,
                    TransferDestination = transfer?.ToBranch?.Name,
                    QueueLength = queue.Count,
                    FirstInQueueBorrowerId = queue.Count > 0 ? queue[0].BorrowerId : null,
                    OnHoldShelf = queue.Any(q => q.Status == ReservationStatus.Ready)
                };
            }

            return states;
        }

        // =====================================================================
        // helpers
        // =====================================================================
        private Task<bool> HasActiveTransferAsync(int itemId) =>
            _context.BranchTransfers.AnyAsync(t =>
                t.ItemId == itemId && (t.Status == TransferStatus.Requested || t.Status == TransferStatus.InTransit));

        private static bool IsActive(Reservation r) =>
            r.Status == ReservationStatus.Waiting || r.Status == ReservationStatus.Ready;

        /// <summary>Queue position (1 = first) of every active hold, counted per item in the order the holds were placed.</summary>
        private static Dictionary<int, int> BuildPositions(IEnumerable<Reservation> activeHolds)
        {
            var positions = new Dictionary<int, int>();
            foreach (var group in activeHolds.GroupBy(r => r.ItemId))
            {
                var place = 1;
                foreach (var hold in group.OrderBy(r => r.Id))
                {
                    positions[hold.Id] = place++;
                }
            }

            return positions;
        }

        private async Task<Dictionary<int, int>> PositionsForItemsAsync(List<int> itemIds)
        {
            var active = await _context.Reservations
                .AsNoTracking()
                .Where(r => itemIds.Contains(r.ItemId) && (r.Status == ReservationStatus.Waiting || r.Status == ReservationStatus.Ready))
                .OrderBy(r => r.Id)
                .ToListAsync();

            return BuildPositions(active);
        }

        private static ReservationRow ToRow(Reservation r, Dictionary<int, int> positions) => new()
        {
            Id = r.Id,
            ItemId = r.ItemId,
            ItemName = r.Item?.Name ?? "(item removed)",
            LibraryCode = r.Item?.LibraryCode ?? string.Empty,
            ItemType = r.Item?.ItemType ?? string.Empty,
            ItemStatus = r.Item?.Status ?? ItemStatus.Available,
            CurrentBranch = r.Item?.Branch?.Name,
            BorrowerId = r.BorrowerId,
            BorrowerName = r.Borrower?.FullName ?? "(borrower removed)",
            CardNumber = r.Borrower?.CardNumber ?? string.Empty,
            Status = r.Status,
            QueuePosition = IsActive(r) && positions.TryGetValue(r.Id, out var position) ? position : null,
            CreatedDate = r.CreatedDate,
            ReadyDate = r.ReadyDate,
            FulfilledDate = r.FulfilledDate,
            CancelledDate = r.CancelledDate,
            PickupBranch = r.PickupBranch?.Name,
            PlacedVia = r.PlacedVia,
            CancelReason = r.CancelReason
        };

        private static string Truncate(string? value, int max)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length <= max ? text : text.Substring(0, max);
        }
    }

    /// <summary>Feeds the patron's holds into the kiosk account page (replaces the "not available yet" placeholder).</summary>
    public sealed class ReservationSummaryProvider : IReservationSummaryProvider
    {
        private readonly IReservationService _reservations;

        public ReservationSummaryProvider(IReservationService reservations)
        {
            _reservations = reservations;
        }

        public bool IsEnabled => true;

        public async Task<IReadOnlyList<KioskReservationInfo>> GetForBorrowerAsync(int borrowerId)
        {
            var rows = await _reservations.GetActiveForBorrowerAsync(borrowerId);
            return rows.Select(r => new KioskReservationInfo
            {
                Id = r.Id,
                ItemId = r.ItemId,
                ItemName = r.ItemName,
                LibraryCode = r.LibraryCode,
                Status = r.Status.ToString(),
                QueuePosition = r.Status == ReservationStatus.Ready ? null : r.QueuePosition,
                PickupBranch = r.Status == ReservationStatus.Ready ? (r.PickupBranch ?? r.CurrentBranch) : null
            }).ToList();
        }
    }
}

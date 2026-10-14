using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bibliotheca.Data;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.Enums;
using Bibliotheca.Models.ViewModels;
using Bibliotheca.Services;

namespace Bibliotheca.Controllers
{
    /// <summary>
    /// Inter-branch transfers. Admin and reception manage them; managers can follow the history.
    /// </summary>
    [Authorize(Roles = "Admin,Reception,Manager")]
    public class TransfersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITransferService _transfers;

        public TransfersController(ApplicationDbContext context, ITransferService transfers)
        {
            _context = context;
            _transfers = transfers;
        }

        // GET: /Transfers?filter=Active|Completed|Cancelled|All&branchId=1
        public async Task<IActionResult> Index(string? filter = "Active", int? branchId = null)
        {
            IQueryable<BranchTransfer> query = _context.BranchTransfers
                .AsNoTracking()
                .Include(t => t.Item)
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch);

            var normalised = "Active";
            if (string.Equals(filter, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                normalised = "Completed";
                query = query.Where(t => t.Status == TransferStatus.Completed);
            }
            else if (string.Equals(filter, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                normalised = "Cancelled";
                query = query.Where(t => t.Status == TransferStatus.Cancelled);
            }
            else if (string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase))
            {
                normalised = "All";
            }
            else
            {
                query = query.Where(t => t.Status == TransferStatus.Requested || t.Status == TransferStatus.InTransit);
            }

            if (branchId.HasValue)
            {
                query = query.Where(t => t.FromBranchId == branchId.Value || t.ToBranchId == branchId.Value);
            }

            var model = new TransferListViewModel
            {
                Filter = normalised,
                BranchId = branchId,
                Branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync(),
                Transfers = await query.OrderByDescending(t => t.Id).ToListAsync()
            };

            return View(model);
        }

        // GET: /Transfers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var transfer = await _context.BranchTransfers
                .AsNoTracking()
                .Include(t => t.Item)
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transfer == null) return NotFound();
            return View(transfer);
        }

        // GET: /Transfers/Create?itemId=3
        [Authorize(Roles = "Admin,Reception")]
        public async Task<IActionResult> Create(int? itemId = null)
        {
            var model = new TransferCreateViewModel { ItemId = itemId };
            await LoadListsAsync(model);
            return View(model);
        }

        // POST: /Transfers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Reception")]
        public async Task<IActionResult> Create(TransferCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _transfers.RequestAsync(model.ItemId!.Value, model.ToBranchId!.Value, model.Notes, User.Identity?.Name);
                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }

                var field = result.Error == TransferError.DestinationInvalid || result.Error == TransferError.SameBranch
                    ? nameof(model.ToBranchId)
                    : nameof(model.ItemId);
                ModelState.AddModelError(field, result.Message);
            }

            await LoadListsAsync(model);
            return View(model);
        }

        // POST: /Transfers/Dispatch/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Reception")]
        public async Task<IActionResult> Dispatch(int id, string? returnTo = null)
        {
            var result = await _transfers.DispatchAsync(id, User.Identity?.Name);
            Report(result);
            return Back(returnTo, id);
        }

        // POST: /Transfers/Complete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Reception")]
        public async Task<IActionResult> Complete(int id, string? returnTo = null)
        {
            var result = await _transfers.CompleteAsync(id, User.Identity?.Name);
            Report(result);
            return Back(returnTo, id);
        }

        // POST: /Transfers/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Reception")]
        public async Task<IActionResult> Cancel(int id, string? reason = null, string? returnTo = null)
        {
            var result = await _transfers.CancelAsync(id, reason, User.Identity?.Name);
            Report(result);
            return Back(returnTo, id);
        }

        private void Report(TransferResult result)
        {
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }
        }

        private IActionResult Back(string? returnTo, int id) =>
            string.Equals(returnTo, "details", StringComparison.OrdinalIgnoreCase)
                ? RedirectToAction(nameof(Details), new { id })
                : RedirectToAction(nameof(Index));

        private async Task LoadListsAsync(TransferCreateViewModel model)
        {
            var busyItemIds = await _context.BranchTransfers
                .Where(t => t.Status == TransferStatus.Requested || t.Status == TransferStatus.InTransit)
                .Select(t => t.ItemId)
                .ToListAsync();

            // Items that can be moved right now: at a branch, not on loan, not destroyed, no transfer already open
            model.Items = await _context.Items
                .AsNoTracking()
                .Include(i => i.Branch)
                .Where(i => i.BranchId != null && i.Status != ItemStatus.Borrowed && i.Status != ItemStatus.Destroyed && !busyItemIds.Contains(i.Id))
                .OrderBy(i => i.LibraryCode)
                .ToListAsync();

            model.Branches = await _context.Branches
                .AsNoTracking()
                .Where(b => b.IsActive)
                .OrderBy(b => b.Name)
                .ToListAsync();
        }
    }
}

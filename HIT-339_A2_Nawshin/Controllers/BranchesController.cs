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
    /// <summary>Branches and the per-branch inventory view for staff.</summary>
    [Authorize(Roles = "Admin,Reception,Manager")]
    public class BranchesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IReservationService _reservations;

        public BranchesController(ApplicationDbContext context, IReservationService reservations)
        {
            _context = context;
            _reservations = reservations;
        }

        // GET: /Branches
        public async Task<IActionResult> Index()
        {
            var branches = await _context.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
            var items = await _context.Items.AsNoTracking().Select(i => new { i.BranchId, i.Status }).ToListAsync();
            var openTransfers = await _context.BranchTransfers
                .AsNoTracking()
                .Where(t => t.Status == TransferStatus.Requested || t.Status == TransferStatus.InTransit)
                .ToListAsync();

            var rows = branches.Select(b =>
            {
                var mine = items.Where(i => i.BranchId == b.Id).ToList();
                return new BranchSummaryRow
                {
                    Branch = b,
                    Total = mine.Count,
                    Available = mine.Count(i => i.Status == ItemStatus.Available),
                    Borrowed = mine.Count(i => i.Status == ItemStatus.Borrowed),
                    Damaged = mine.Count(i => i.Status == ItemStatus.Damaged),
                    Destroyed = mine.Count(i => i.Status == ItemStatus.Destroyed),
                    IncomingTransfers = openTransfers.Count(t => t.ToBranchId == b.Id),
                    OutgoingTransfers = openTransfers.Count(t => t.FromBranchId == b.Id)
                };
            }).ToList();

            ViewBag.UnassignedItems = items.Count(i => i.BranchId == null);
            return View(rows);
        }

        // GET: /Branches/Inventory?branchId=1&category=Book&status=Available&searchTerm=hobbit
        public async Task<IActionResult> Inventory(int? branchId = null, string? category = "All", ItemStatus? status = null, string? searchTerm = null)
        {
            IQueryable<Item> query = _context.Items.AsNoTracking().Include(i => i.Branch);

            if (branchId.HasValue)
            {
                query = query.Where(i => i.BranchId == branchId.Value);
            }

            query = query.ApplyCategory(category);

            if (status.HasValue)
            {
                query = query.Where(i => i.Status == status.Value);
            }

            query = query.ApplyKeywordSearch(searchTerm);

            var items = await query.OrderBy(i => i.Branch!.Name).ThenBy(i => i.LibraryCode).ToListAsync();

            var model = new BranchInventoryViewModel
            {
                BranchId = branchId,
                Category = category,
                Status = status,
                SearchTerm = searchTerm,
                Branches = await _context.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync(),
                Items = items,
                LendingStates = await _reservations.GetLendingStatesAsync(items.Select(i => i.Id))
            };

            return View(model);
        }

        // GET: /Branches/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View(new Branch());
        }

        // POST: /Branches/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Name,Code,Address,IsActive")] Branch branch)
        {
            await ValidateUniqueAsync(branch, null);

            if (ModelState.IsValid)
            {
                branch.Name = branch.Name.Trim();
                branch.Code = branch.Code.Trim().ToUpperInvariant();
                _context.Branches.Add(branch);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Branch '{branch.Name}' was created.";
                return RedirectToAction(nameof(Index));
            }

            return View(branch);
        }

        // GET: /Branches/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();
            return View(branch);
        }

        // POST: /Branches/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Code,Address,IsActive")] Branch branch)
        {
            if (id != branch.Id) return NotFound();

            await ValidateUniqueAsync(branch, branch.Id);

            if (ModelState.IsValid)
            {
                var existing = await _context.Branches.FirstOrDefaultAsync(b => b.Id == id);
                if (existing == null) return NotFound();

                existing.Name = branch.Name.Trim();
                existing.Code = branch.Code.Trim().ToUpperInvariant();
                existing.Address = string.IsNullOrWhiteSpace(branch.Address) ? null : branch.Address.Trim();
                existing.IsActive = branch.IsActive;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Branch '{existing.Name}' was updated.";
                return RedirectToAction(nameof(Index));
            }

            return View(branch);
        }

        private async Task ValidateUniqueAsync(Branch branch, int? ignoreId)
        {
            var name = (branch.Name ?? string.Empty).Trim().ToUpperInvariant();
            var code = (branch.Code ?? string.Empty).Trim().ToUpperInvariant();

            if (name.Length > 0 &&
                await _context.Branches.AnyAsync(b => b.Name.ToUpper() == name && (!ignoreId.HasValue || b.Id != ignoreId.Value)))
            {
                ModelState.AddModelError(nameof(Branch.Name), "A branch with this name already exists.");
            }

            if (code.Length > 0 &&
                await _context.Branches.AnyAsync(b => b.Code.ToUpper() == code && (!ignoreId.HasValue || b.Id != ignoreId.Value)))
            {
                ModelState.AddModelError(nameof(Branch.Code), "A branch with this code already exists.");
            }
        }
    }
}

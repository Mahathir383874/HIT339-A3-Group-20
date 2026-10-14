using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bibliotheca.Data;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.Enums;
using Bibliotheca.Services;

namespace Bibliotheca.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ItemsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IReservationService _reservations;
        private readonly ITransferService _transfers;

        public ItemsController(
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            IReservationService reservations,
            ITransferService transfers)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _reservations = reservations;
            _transfers = transfers;
        }

        // GET: /Items
        public async Task<IActionResult> Index(string? category = "All", string? searchTerm = null, ItemStatus? status = null, int? branchId = null)
        {
            IQueryable<Item> query = _context.Items.Include(i => i.Branch);

            if (branchId.HasValue)
            {
                query = query.Where(i => i.BranchId == branchId.Value);
            }

            if (!string.IsNullOrEmpty(category) && category != "All")
            {
                query = query.Where(i => i.ItemType == category);
            }

            if (status.HasValue)
            {
                query = query.Where(i => i.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(i =>
                    i.Name.ToLower().Contains(term) ||
                    i.LibraryCode.ToLower().Contains(term) ||
                    (i.Description != null && i.Description.ToLower().Contains(term)));
            }

            ViewBag.ActiveCategory = category;
            ViewBag.SearchTerm = searchTerm;
            ViewBag.Status = status;
            ViewBag.BranchId = branchId;
            ViewBag.Branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();

            return View(await query.OrderBy(i => i.LibraryCode).ToListAsync());
        }

        // GET: /Items/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var item = await _context.Items
                .Include(i => i.Branch)
                .Include(i => i.Loans)
                    .ThenInclude(l => l.Borrower)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            // Hold queue and transfer history for this item
            ViewBag.Queue = await _reservations.GetQueueForItemAsync(item.Id);
            ViewBag.Transfers = await _context.BranchTransfers
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .Where(t => t.ItemId == item.Id)
                .OrderByDescending(t => t.Id)
                .ToListAsync();

            return View(item);
        }

        // --- CREATE ACTIONS ---

        // GET: /Items/CreateBook
        public async Task<IActionResult> CreateBook()
        {
            var defaultBranchId = await LoadBranchesAsync(null);
            return View(new Book { ReplacementCost = 25.00m, BranchId = defaultBranchId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBook(Book book, IFormFile? imageFile)
        {
            // Check for duplicate Library Code
            if (await _context.Items.AnyAsync(i => i.LibraryCode == book.LibraryCode))
            {
                ModelState.AddModelError("LibraryCode", "A library item with this accession code already exists.");
            }

            await ValidateBranchAsync(book);

            if (ModelState.IsValid)
            {
                book.ImageUrl = await SaveImageFileAsync(imageFile, book.ImageUrl);
                book.CreatedDate = DateTime.UtcNow;
                _context.Add(book);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Book '{book.Name}' ({book.LibraryCode}) created successfully.";
                return RedirectToAction(nameof(Index), new { category = "Book" });
            }

            await LoadBranchesAsync(book.BranchId);
            return View(book);
        }

        // GET: /Items/CreateMusic
        public async Task<IActionResult> CreateMusic()
        {
            var defaultBranchId = await LoadBranchesAsync(null);
            return View(new Music { ReplacementCost = 25.00m, BranchId = defaultBranchId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMusic(Music music, IFormFile? imageFile)
        {
            if (await _context.Items.AnyAsync(i => i.LibraryCode == music.LibraryCode))
            {
                ModelState.AddModelError("LibraryCode", "A library item with this accession code already exists.");
            }

            await ValidateBranchAsync(music);

            if (ModelState.IsValid)
            {
                music.ImageUrl = await SaveImageFileAsync(imageFile, music.ImageUrl);
                music.CreatedDate = DateTime.UtcNow;
                _context.Add(music);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Music album '{music.Name}' ({music.LibraryCode}) created successfully.";
                return RedirectToAction(nameof(Index), new { category = "Music" });
            }

            await LoadBranchesAsync(music.BranchId);
            return View(music);
        }

        // GET: /Items/CreateToy
        public async Task<IActionResult> CreateToy()
        {
            var defaultBranchId = await LoadBranchesAsync(null);
            return View(new Toy { ReplacementCost = 35.00m, BranchId = defaultBranchId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateToy(Toy toy, IFormFile? imageFile)
        {
            if (await _context.Items.AnyAsync(i => i.LibraryCode == toy.LibraryCode))
            {
                ModelState.AddModelError("LibraryCode", "A library item with this accession code already exists.");
            }

            await ValidateBranchAsync(toy);

            if (ModelState.IsValid)
            {
                toy.ImageUrl = await SaveImageFileAsync(imageFile, toy.ImageUrl);
                toy.CreatedDate = DateTime.UtcNow;
                _context.Add(toy);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Toy '{toy.Name}' ({toy.LibraryCode}) created successfully.";
                return RedirectToAction(nameof(Index), new { category = "Toy" });
            }

            await LoadBranchesAsync(toy.BranchId);
            return View(toy);
        }

        // --- EDIT ACTIONS ---

        // GET: /Items/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var item = await _context.Items.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            return item switch
            {
                Book book => RedirectToAction(nameof(EditBook), new { id = book.Id }),
                Music music => RedirectToAction(nameof(EditMusic), new { id = music.Id }),
                Toy toy => RedirectToAction(nameof(EditToy), new { id = toy.Id }),
                _ => NotFound()
            };
        }

        // GET: /Items/EditBook/5
        public async Task<IActionResult> EditBook(int? id)
        {
            if (id == null) return NotFound();
            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();
            await LoadBranchesAsync(book.BranchId);
            return View(book);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBook(int id, Book book, IFormFile? imageFile)
        {
            if (id != book.Id) return NotFound();

            if (await _context.Items.AnyAsync(i => i.LibraryCode == book.LibraryCode && i.Id != book.Id))
            {
                ModelState.AddModelError("LibraryCode", "Another item already uses this library code.");
            }

            await ValidateBranchAsync(book);
            var previous = await GetPreviousStateAsync(book.Id);
            await ValidateBranchChangeAsync(book, previous);

            if (ModelState.IsValid)
            {
                try
                {
                    book.ImageUrl = await SaveImageFileAsync(imageFile, book.ImageUrl);
                    _context.Update(book);
                    await _context.SaveChangesAsync();
                    await AfterStatusEditAsync(book.Id, previous?.Status, book.Status);
                    TempData["SuccessMessage"] = $"Book '{book.Name}' updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Books.Any(e => e.Id == book.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await LoadBranchesAsync(book.BranchId);
            return View(book);
        }

        // GET: /Items/EditMusic/5
        public async Task<IActionResult> EditMusic(int? id)
        {
            if (id == null) return NotFound();
            var music = await _context.Musics.FindAsync(id);
            if (music == null) return NotFound();
            await LoadBranchesAsync(music.BranchId);
            return View(music);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMusic(int id, Music music, IFormFile? imageFile)
        {
            if (id != music.Id) return NotFound();

            if (await _context.Items.AnyAsync(i => i.LibraryCode == music.LibraryCode && i.Id != music.Id))
            {
                ModelState.AddModelError("LibraryCode", "Another item already uses this library code.");
            }

            await ValidateBranchAsync(music);
            var previous = await GetPreviousStateAsync(music.Id);
            await ValidateBranchChangeAsync(music, previous);

            if (ModelState.IsValid)
            {
                try
                {
                    music.ImageUrl = await SaveImageFileAsync(imageFile, music.ImageUrl);
                    _context.Update(music);
                    await _context.SaveChangesAsync();
                    await AfterStatusEditAsync(music.Id, previous?.Status, music.Status);
                    TempData["SuccessMessage"] = $"Music item '{music.Name}' updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Musics.Any(e => e.Id == music.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await LoadBranchesAsync(music.BranchId);
            return View(music);
        }

        // GET: /Items/EditToy/5
        public async Task<IActionResult> EditToy(int? id)
        {
            if (id == null) return NotFound();
            var toy = await _context.Toys.FindAsync(id);
            if (toy == null) return NotFound();
            await LoadBranchesAsync(toy.BranchId);
            return View(toy);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditToy(int id, Toy toy, IFormFile? imageFile)
        {
            if (id != toy.Id) return NotFound();

            if (await _context.Items.AnyAsync(i => i.LibraryCode == toy.LibraryCode && i.Id != toy.Id))
            {
                ModelState.AddModelError("LibraryCode", "Another item already uses this library code.");
            }

            await ValidateBranchAsync(toy);
            var previous = await GetPreviousStateAsync(toy.Id);
            await ValidateBranchChangeAsync(toy, previous);

            if (ModelState.IsValid)
            {
                try
                {
                    toy.ImageUrl = await SaveImageFileAsync(imageFile, toy.ImageUrl);
                    _context.Update(toy);
                    await _context.SaveChangesAsync();
                    await AfterStatusEditAsync(toy.Id, previous?.Status, toy.Status);
                    TempData["SuccessMessage"] = $"Toy '{toy.Name}' updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Toys.Any(e => e.Id == toy.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await LoadBranchesAsync(toy.BranchId);
            return View(toy);
        }

        // --- DELETE ACTIONS ---

        // GET: /Items/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Items
                .Include(i => i.Loans)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null) return NotFound();

            ViewBag.HasActiveLoans = item.Loans.Any(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue);
            return View(item);
        }

        // POST: /Items/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.Items
                .Include(i => i.Loans)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item != null)
            {
                if (item.Loans.Any(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue))
                {
                    TempData["ErrorMessage"] = "Cannot delete an item that is currently borrowed or overdue. Please return the item first.";
                    return RedirectToAction(nameof(Index));
                }

                var activeHolds = await _reservations.GetQueueLengthAsync(item.Id);
                if (activeHolds > 0)
                {
                    TempData["ErrorMessage"] = $"Cannot delete '{item.Name}' while {activeHolds} patron(s) have an active hold on it. Cancel the reservations first.";
                    return RedirectToAction(nameof(Index));
                }

                if (await _transfers.HasActiveTransferAsync(item.Id))
                {
                    TempData["ErrorMessage"] = $"Cannot delete '{item.Name}' while it has an inter-branch transfer in progress. Complete or cancel the transfer first.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Items.Remove(item);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Item '{item.Name}' deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        // --- HELPERS: branches, status changes ---

        /// <summary>Fills ViewBag.Branches for the branch drop-down and returns the first active branch (the default for new items).</summary>
        private async Task<int?> LoadBranchesAsync(int? selectedBranchId)
        {
            var branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
            var choices = branches.Where(b => b.IsActive || b.Id == selectedBranchId).ToList();
            ViewBag.Branches = choices;
            return choices.FirstOrDefault(b => b.IsActive)?.Id;
        }

        private async Task ValidateBranchAsync(Item item)
        {
            if (!item.BranchId.HasValue)
            {
                ModelState.AddModelError(nameof(Item.BranchId), "Please choose the branch that holds this item.");
                return;
            }

            if (!await _context.Branches.AnyAsync(b => b.Id == item.BranchId.Value))
            {
                ModelState.AddModelError(nameof(Item.BranchId), "The selected branch does not exist.");
            }
        }

        private sealed record PreviousItemState(ItemStatus Status, int? BranchId);

        private async Task<PreviousItemState?> GetPreviousStateAsync(int itemId)
        {
            var row = await _context.Items
                .AsNoTracking()
                .Where(i => i.Id == itemId)
                .Select(i => new { i.Status, i.BranchId })
                .FirstOrDefaultAsync();

            return row == null ? null : new PreviousItemState(row.Status, row.BranchId);
        }

        /// <summary>An item that is part of a transfer in progress must not be moved by editing its branch.</summary>
        private async Task ValidateBranchChangeAsync(Item item, PreviousItemState? previous)
        {
            if (previous == null || previous.BranchId == item.BranchId) return;

            if (await _transfers.HasActiveTransferAsync(item.Id))
            {
                ModelState.AddModelError(nameof(Item.BranchId),
                    "This item has an inter-branch transfer in progress, so its branch cannot be edited. Complete or cancel the transfer first.");
            }
        }

        /// <summary>
        /// When staff change an item's status by hand the hold queue has to follow: an item set back to Available
        /// serves the next waiting patron, and a destroyed item cancels its holds.
        /// </summary>
        private async Task AfterStatusEditAsync(int itemId, ItemStatus? previousStatus, ItemStatus newStatus)
        {
            if (previousStatus == newStatus) return;
            if (newStatus != ItemStatus.Available && newStatus != ItemStatus.Destroyed) return;

            var promoted = await _reservations.AdvanceQueueAsync(itemId);
            if (promoted?.Borrower != null)
            {
                TempData["InfoMessage"] = $"Hold queue advanced: {promoted.Borrower.FullName} is next in line and was notified that the item is ready.";
            }
        }

        // --- HELPER: Process Device File Upload or URL ---
        private async Task<string?> SaveImageFileAsync(IFormFile? imageFile, string? providedUrl)
        {
            if (imageFile != null && imageFile.Length > 0)
            {
                var webRoot = _webHostEnvironment.WebRootPath 
                    ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var uploadsFolder = Path.Combine(webRoot, "uploads", "items");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var extension = Path.GetExtension(imageFile.FileName);
                var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                return $"/uploads/items/{uniqueFileName}";
            }

            return string.IsNullOrWhiteSpace(providedUrl) ? null : providedUrl.Trim();
        }
    }
}

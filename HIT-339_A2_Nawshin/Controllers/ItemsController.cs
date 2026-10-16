using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bibliotheca.Data;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.Enums;

namespace Bibliotheca.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ItemsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ItemsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: /Items
        public async Task<IActionResult> Index(string? category = "All", string? searchTerm = null, ItemStatus? status = null)
        {
            IQueryable<Item> query = _context.Items;

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
                .Include(i => i.Loans)
                    .ThenInclude(l => l.Borrower)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            return View(item);
        }

        // --- CREATE ACTIONS ---

        // GET: /Items/CreateBook
        public IActionResult CreateBook()
        {
            return View(new Book { ReplacementCost = 25.00m });
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

            if (ModelState.IsValid)
            {
                book.ImageUrl = await SaveImageFileAsync(imageFile, book.ImageUrl);
                book.CreatedDate = DateTime.UtcNow;
                _context.Add(book);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Book '{book.Name}' ({book.LibraryCode}) created successfully.";
                return RedirectToAction(nameof(Index), new { category = "Book" });
            }
            return View(book);
        }

        // GET: /Items/CreateMusic
        public IActionResult CreateMusic()
        {
            return View(new Music { ReplacementCost = 25.00m });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMusic(Music music, IFormFile? imageFile)
        {
            if (await _context.Items.AnyAsync(i => i.LibraryCode == music.LibraryCode))
            {
                ModelState.AddModelError("LibraryCode", "A library item with this accession code already exists.");
            }

            if (ModelState.IsValid)
            {
                music.ImageUrl = await SaveImageFileAsync(imageFile, music.ImageUrl);
                music.CreatedDate = DateTime.UtcNow;
                _context.Add(music);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Music album '{music.Name}' ({music.LibraryCode}) created successfully.";
                return RedirectToAction(nameof(Index), new { category = "Music" });
            }
            return View(music);
        }

        // GET: /Items/CreateToy
        public IActionResult CreateToy()
        {
            return View(new Toy { ReplacementCost = 35.00m });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateToy(Toy toy, IFormFile? imageFile)
        {
            if (await _context.Items.AnyAsync(i => i.LibraryCode == toy.LibraryCode))
            {
                ModelState.AddModelError("LibraryCode", "A library item with this accession code already exists.");
            }

            if (ModelState.IsValid)
            {
                toy.ImageUrl = await SaveImageFileAsync(imageFile, toy.ImageUrl);
                toy.CreatedDate = DateTime.UtcNow;
                _context.Add(toy);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Toy '{toy.Name}' ({toy.LibraryCode}) created successfully.";
                return RedirectToAction(nameof(Index), new { category = "Toy" });
            }
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

            if (ModelState.IsValid)
            {
                try
                {
                    book.ImageUrl = await SaveImageFileAsync(imageFile, book.ImageUrl);
                    _context.Update(book);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Book '{book.Name}' updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Books.Any(e => e.Id == book.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(book);
        }

        // GET: /Items/EditMusic/5
        public async Task<IActionResult> EditMusic(int? id)
        {
            if (id == null) return NotFound();
            var music = await _context.Musics.FindAsync(id);
            if (music == null) return NotFound();
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

            if (ModelState.IsValid)
            {
                try
                {
                    music.ImageUrl = await SaveImageFileAsync(imageFile, music.ImageUrl);
                    _context.Update(music);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Music item '{music.Name}' updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Musics.Any(e => e.Id == music.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(music);
        }

        // GET: /Items/EditToy/5
        public async Task<IActionResult> EditToy(int? id)
        {
            if (id == null) return NotFound();
            var toy = await _context.Toys.FindAsync(id);
            if (toy == null) return NotFound();
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

            if (ModelState.IsValid)
            {
                try
                {
                    toy.ImageUrl = await SaveImageFileAsync(imageFile, toy.ImageUrl);
                    _context.Update(toy);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Toy '{toy.Name}' updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Toys.Any(e => e.Id == toy.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
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

                _context.Items.Remove(item);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Item '{item.Name}' deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bibliotheca.Data;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.Enums;

namespace Bibliotheca.Controllers
{
    [Authorize(Roles = "Reception,Admin")]
    public class BorrowersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BorrowersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Borrowers
        public async Task<IActionResult> Index(string? searchTerm, bool? activeOnly)
        {
            IQueryable<Borrower> query = _context.Borrowers
                .Include(b => b.Loans)
                .Include(b => b.Fines);

            if (activeOnly.HasValue && activeOnly.Value)
            {
                query = query.Where(b => b.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(b =>
                    b.FirstName.ToLower().Contains(term) ||
                    b.LastName.ToLower().Contains(term) ||
                    b.CardNumber.ToLower().Contains(term) ||
                    b.Email.ToLower().Contains(term) ||
                    b.Phone.Contains(term));
            }

            ViewBag.SearchTerm = searchTerm;
            ViewBag.ActiveOnly = activeOnly ?? false;

            return View(await query.OrderBy(b => b.LastName).ThenBy(b => b.FirstName).ToListAsync());
        }

        // GET: /Borrowers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var borrower = await _context.Borrowers
                .Include(b => b.Loans)
                    .ThenInclude(l => l.Item)
                .Include(b => b.Loans)
                    .ThenInclude(l => l.Fine)
                .Include(b => b.Fines)
                    .ThenInclude(f => f.Loan)
                        .ThenInclude(l => l!.Item)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (borrower == null) return NotFound();

            return View(borrower);
        }

        // GET: /Borrowers/Create
        public IActionResult Create()
        {
            // Suggest next available card number
            var maxId = _context.Borrowers.Any() ? _context.Borrowers.Max(b => b.Id) : 0;
            var suggestedCardNumber = $"CRD-{1000 + maxId + 1}";

            var borrower = new Borrower
            {
                CardNumber = suggestedCardNumber,
                RegisteredDate = DateTime.UtcNow,
                IsActive = true
            };
            return View(borrower);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Borrower borrower)
        {
            if (await _context.Borrowers.AnyAsync(b => b.CardNumber == borrower.CardNumber))
            {
                ModelState.AddModelError("CardNumber", "A borrower with this card number already exists.");
            }

            if (await _context.Borrowers.AnyAsync(b => b.Email == borrower.Email))
            {
                ModelState.AddModelError("Email", "A borrower with this email address is already registered.");
            }

            if (ModelState.IsValid)
            {
                borrower.RegisteredDate = DateTime.UtcNow;
                _context.Add(borrower);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Borrower '{borrower.FullName}' registered successfully (Card: {borrower.CardNumber}).";
                return RedirectToAction(nameof(Details), new { id = borrower.Id });
            }
            return View(borrower);
        }

        // GET: /Borrowers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var borrower = await _context.Borrowers.FindAsync(id);
            if (borrower == null) return NotFound();

            return View(borrower);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Borrower borrower)
        {
            if (id != borrower.Id) return NotFound();

            if (await _context.Borrowers.AnyAsync(b => b.CardNumber == borrower.CardNumber && b.Id != borrower.Id))
            {
                ModelState.AddModelError("CardNumber", "Another borrower already has this card number.");
            }

            if (await _context.Borrowers.AnyAsync(b => b.Email == borrower.Email && b.Id != borrower.Id))
            {
                ModelState.AddModelError("Email", "Another borrower is registered with this email address.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(borrower);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Borrower '{borrower.FullName}' updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Borrowers.Any(e => e.Id == borrower.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Details), new { id = borrower.Id });
            }
            return View(borrower);
        }

        // GET: /Borrowers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var borrower = await _context.Borrowers
                .Include(b => b.Loans)
                .Include(b => b.Fines)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (borrower == null) return NotFound();

            ViewBag.HasActiveLoans = borrower.Loans.Any(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue);
            ViewBag.HasUnpaidFines = borrower.Fines.Any(f => !f.IsPaid);

            return View(borrower);
        }

        // POST: /Borrowers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var borrower = await _context.Borrowers
                .Include(b => b.Loans)
                .Include(b => b.Fines)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (borrower != null)
            {
                if (borrower.Loans.Any(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue))
                {
                    TempData["ErrorMessage"] = "Cannot delete a borrower with active or overdue loans. Please return items first.";
                    return RedirectToAction(nameof(Details), new { id = borrower.Id });
                }

                if (borrower.Fines.Any(f => !f.IsPaid))
                {
                    TempData["ErrorMessage"] = "Cannot delete a borrower with unpaid overdue fines.";
                    return RedirectToAction(nameof(Details), new { id = borrower.Id });
                }

                _context.Borrowers.Remove(borrower);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Borrower '{borrower.FullName}' removed successfully.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

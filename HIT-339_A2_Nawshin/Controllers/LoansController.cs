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
    [Authorize(Roles = "Reception,Admin")]
    public class LoansController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICirculationService _circulation;
        private const decimal DAILY_FINE_RATE = 1.00m; // $1.00 per day overdue

        public LoansController(ApplicationDbContext context, ICirculationService circulation)
        {
            _context = context;
            _circulation = circulation;
        }

        // GET: /Loans
        public async Task<IActionResult> Index(string? filter = "Active", string? searchTerm = null)
        {
            IQueryable<Loan> query = _context.Loans
                .Include(l => l.Item)
                .Include(l => l.Borrower)
                .Include(l => l.Fine);

            // Filter by status tab
            query = filter switch
            {
                "Active" => query.Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue),
                "Overdue" => query.Where(l => l.Status == LoanStatus.Overdue || (l.Status == LoanStatus.Active && l.DueDate < DateTime.UtcNow)),
                "Returned" => query.Where(l => l.Status == LoanStatus.Returned),
                _ => query
            };

            // Search filter
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(l =>
                    l.Item!.Name.ToLower().Contains(term) ||
                    l.Item.LibraryCode.ToLower().Contains(term) ||
                    l.Borrower!.FirstName.ToLower().Contains(term) ||
                    l.Borrower.LastName.ToLower().Contains(term) ||
                    l.Borrower.CardNumber.ToLower().Contains(term));
            }

            ViewBag.ActiveFilter = filter;
            ViewBag.SearchTerm = searchTerm;

            var loans = await query.OrderByDescending(l => l.BorrowDate).ToListAsync();

            // Refresh Overdue status for active loans whose due date has passed
            foreach (var loan in loans.Where(l => l.Status == LoanStatus.Active && l.DueDate < DateTime.UtcNow))
            {
                loan.Status = LoanStatus.Overdue;
            }

            return View(loans);
        }

        // GET: /Loans/Issue
        public async Task<IActionResult> Issue(string? libraryCode = null, string? cardNumber = null)
        {
            var availableItems = await _context.Items
                .Where(i => i.Status == ItemStatus.Available)
                .OrderBy(i => i.Name)
                .ToListAsync();

            var activeBorrowers = await _context.Borrowers
                .Where(b => b.IsActive)
                .OrderBy(b => b.LastName)
                .ToListAsync();

            var model = new IssueLoanViewModel
            {
                LibraryCode = libraryCode ?? string.Empty,
                CardNumber = cardNumber ?? string.Empty,
                LoanDays = 14,
                AvailableItems = availableItems,
                ActiveBorrowers = activeBorrowers
            };

            return View(model);
        }

        // POST: /Loans/Issue
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Issue(IssueLoanViewModel model)
        {
            // Re-populate dropdown items for redisplay in case of error
            model.AvailableItems = await _context.Items
                .Where(i => i.Status == ItemStatus.Available)
                .OrderBy(i => i.Name)
                .ToListAsync();

            model.ActiveBorrowers = await _context.Borrowers
                .Where(b => b.IsActive)
                .OrderBy(b => b.LastName)
                .ToListAsync();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Borrower/item checks and loan creation live in the shared CirculationService,
            // which is also used by the Public Self-Service Kiosk.
            var result = await _circulation.CheckoutAsync(new CheckoutRequest
            {
                CardNumber = model.CardNumber,
                LibraryCode = model.LibraryCode,
                LoanDays = model.LoanDays,
                Remarks = model.Remarks
            });

            if (!result.Succeeded)
            {
                var field = result.Error switch
                {
                    CheckoutError.CardNotFound or CheckoutError.BorrowerInactive => nameof(model.CardNumber),
                    CheckoutError.InvalidLoanDays => nameof(model.LoanDays),
                    _ => nameof(model.LibraryCode)
                };
                ModelState.AddModelError(field, result.Message);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // GET: /Loans/Return/5
        public async Task<IActionResult> Return(int? id)
        {
            if (id == null) return NotFound();

            var loan = await _context.Loans
                .Include(l => l.Item)
                .Include(l => l.Borrower)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (loan == null) return NotFound();

            if (loan.Status == LoanStatus.Returned)
            {
                TempData["InfoMessage"] = "This item has already been returned.";
                return RedirectToAction(nameof(Index));
            }

            var returnDate = DateTime.UtcNow;
            int daysLate = 0;
            decimal fineAmount = 0m;

            if (returnDate > loan.DueDate)
            {
                daysLate = (int)Math.Ceiling((returnDate - loan.DueDate).TotalDays);
                if (daysLate < 1) daysLate = 1;
                fineAmount = daysLate * DAILY_FINE_RATE;
            }

            var model = new ProcessReturnViewModel
            {
                LoanId = loan.Id,
                ItemName = loan.Item?.Name ?? "Unknown Item",
                LibraryCode = loan.Item?.LibraryCode ?? "N/A",
                ItemType = loan.Item?.ItemType ?? "Item",
                BorrowerName = loan.Borrower?.FullName ?? "Unknown Borrower",
                CardNumber = loan.Borrower?.CardNumber ?? "N/A",
                BorrowDate = loan.BorrowDate,
                DueDate = loan.DueDate,
                ReturnDate = returnDate,
                DaysLate = daysLate,
                CalculatedFine = fineAmount,
                NewItemStatus = ItemStatus.Available,
                PayFineImmediately = fineAmount > 0
            };

            return View(model);
        }

        // POST: /Loans/Return
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Return(ProcessReturnViewModel model)
        {
            var loan = await _context.Loans
                .Include(l => l.Item)
                .Include(l => l.Borrower)
                .FirstOrDefaultAsync(l => l.Id == model.LoanId);

            if (loan == null) return NotFound();

            var returnDate = DateTime.UtcNow;
            loan.ReturnDate = returnDate;
            loan.Status = LoanStatus.Returned;
            loan.Remarks = string.IsNullOrWhiteSpace(model.Remarks) ? loan.Remarks : $"{loan.Remarks} | Return note: {model.Remarks}";

            // Update item condition
            if (loan.Item != null)
            {
                loan.Item.Status = model.NewItemStatus;
            }

            // Handle Overdue Late Fine
            if (returnDate > loan.DueDate)
            {
                int daysLate = (int)Math.Ceiling((returnDate - loan.DueDate).TotalDays);
                if (daysLate < 1) daysLate = 1;
                decimal fineAmount = daysLate * DAILY_FINE_RATE;

                var fine = new Fine
                {
                    LoanId = loan.Id,
                    BorrowerId = loan.BorrowerId,
                    Amount = fineAmount,
                    DaysLate = daysLate,
                    CalculationDetails = $"{daysLate} days overdue @ ${DAILY_FINE_RATE:F2}/day",
                    CreatedDate = returnDate,
                    IsPaid = model.PayFineImmediately,
                    PaidDate = model.PayFineImmediately ? returnDate : null
                };

                _context.Fines.Add(fine);

                if (model.PayFineImmediately)
                {
                    TempData["SuccessMessage"] = $"Item returned successfully. Late fine of ${fineAmount:F2} ({daysLate} days overdue) was marked as PAID.";
                }
                else
                {
                    TempData["WarningMessage"] = $"Item returned. Outstanding late fine of ${fineAmount:F2} recorded for {loan.Borrower?.FullName}.";
                }
            }
            else
            {
                TempData["SuccessMessage"] = $"Item '{loan.Item?.Name}' returned successfully on time. No fines incurred.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Loans/Fines
        public async Task<IActionResult> Fines(string? statusFilter = "All")
        {
            IQueryable<Fine> query = _context.Fines
                .Include(f => f.Borrower)
                .Include(f => f.Loan)
                    .ThenInclude(l => l!.Item);

            if (statusFilter == "Unpaid")
            {
                query = query.Where(f => !f.IsPaid);
            }
            else if (statusFilter == "Paid")
            {
                query = query.Where(f => f.IsPaid);
            }

            ViewBag.StatusFilter = statusFilter;
            return View(await query.OrderByDescending(f => f.CreatedDate).ToListAsync());
        }

        // POST: /Loans/PayFine/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayFine(int id)
        {
            var fine = await _context.Fines
                .Include(f => f.Borrower)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (fine == null) return NotFound();

            if (fine.IsPaid)
            {
                TempData["InfoMessage"] = "This fine has already been paid.";
                return RedirectToAction(nameof(Fines));
            }

            fine.IsPaid = true;
            fine.PaidDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Fine of ${fine.Amount:F2} for borrower {fine.Borrower?.FullName} marked as paid.";
            return RedirectToAction(nameof(Fines));
        }
    }
}

using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Bibliotheca.Data;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.Enums;
using Bibliotheca.Models.ViewModels;
using Bibliotheca.Services;

namespace Bibliotheca.Controllers
{
    /// <summary>
    /// Public Self-Service Kiosk (route: /Kiosk).
    /// A simplified, touch-friendly UI for in-library kiosk stations. Patrons identify themselves
    /// by scanning/typing their library card, then can view their account, search the catalogue
    /// and check out available items. The patron "session" lives in ASP.NET Core Session state,
    /// is ended by the End Session button or automatically after inactivity, and is never shared
    /// with the staff login (ASP.NET Identity) used by the rest of the system.
    /// All kiosk actions are public (no staff login), except TestCards which is staff-only.
    /// </summary>
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)] // Back button can't reveal a previous patron
    public class KioskController : Controller
    {
        internal const string BorrowerIdKey = "Kiosk.BorrowerId";
        internal const string LastActivityKey = "Kiosk.LastActivity";
        internal const string FailedAttemptsKey = "Kiosk.FailedAttempts";
        internal const string LockedUntilKey = "Kiosk.LockedUntil";

        private readonly ApplicationDbContext _context;
        private readonly ICirculationService _circulation;
        private readonly IReservationSummaryProvider _reservations;
        private readonly KioskOptions _options;
        private readonly ILogger<KioskController> _logger;

        public KioskController(
            ApplicationDbContext context,
            ICirculationService circulation,
            IReservationSummaryProvider reservations,
            IOptions<KioskOptions> options,
            ILogger<KioskController> logger)
        {
            _context = context;
            _circulation = circulation;
            _reservations = reservations;
            _options = options.Value;
            _logger = logger;
        }

        // ------------------------------------------------------------------
        // Start screen: identify patron
        // ------------------------------------------------------------------

        // GET: /Kiosk
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (await GetCurrentBorrowerAsync() != null)
            {
                return RedirectToAction(nameof(Account));
            }

            var model = new KioskStartViewModel();
            ApplyLockoutState(model);
            return View(model);
        }

        // POST: /Kiosk
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(KioskStartViewModel model)
        {
            ClearPatron(); // A new card scan always replaces any previous patron.

            ApplyLockoutState(model);
            if (model.IsLockedOut)
            {
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var borrower = await _circulation.FindBorrowerByCardAsync(model.CardNumber);

            if (borrower == null)
            {
                RegisterFailedAttempt();
                ApplyLockoutState(model);
                model.ErrorMessage ??= "We couldn't find that library card. Check the number and try again, or ask at the front desk.";
                _logger.LogInformation("Kiosk: unknown card number entered.");
                return View(model);
            }

            if (!borrower.IsActive)
            {
                model.ErrorMessage = "This library card is not active. Please see the front desk for help.";
                return View(model);
            }

            HttpContext.Session.Remove(FailedAttemptsKey);
            HttpContext.Session.Remove(LockedUntilKey);
            HttpContext.Session.SetInt32(BorrowerIdKey, borrower.Id);
            Touch();

            _logger.LogInformation("Kiosk: session started for borrower {BorrowerId}.", borrower.Id);
            return RedirectToAction(nameof(Account));
        }

        // ------------------------------------------------------------------
        // Account summary
        // ------------------------------------------------------------------

        // GET: /Kiosk/Account
        [HttpGet]
        public async Task<IActionResult> Account()
        {
            var borrower = await GetCurrentBorrowerAsync();
            if (borrower == null) return SessionEnded();

            var now = DateTime.UtcNow;
            var loans = await _circulation.GetCurrentLoansAsync(borrower.Id);
            var unpaidFines = await _circulation.GetUnpaidFinesAsync(borrower.Id);

            var loanRows = loans.Select(l =>
            {
                var accruing = _circulation.CalculateFine(l.DueDate, now, out var daysLate);
                return new KioskLoanRow
                {
                    LoanId = l.Id,
                    ItemName = l.Item?.Name ?? "Unknown item",
                    LibraryCode = l.Item?.LibraryCode ?? "",
                    ItemType = l.Item?.ItemType ?? "",
                    ImageUrl = l.Item?.ImageUrl,
                    BorrowDate = l.BorrowDate,
                    DueDate = l.DueDate,
                    IsOverdue = daysLate > 0,
                    DaysOverdue = daysLate,
                    DaysUntilDue = daysLate > 0 ? 0 : (int)Math.Ceiling((l.DueDate - now).TotalDays),
                    AccruingFine = accruing
                };
            }).ToList();

            var model = new KioskAccountViewModel
            {
                FirstName = borrower.FirstName,
                DisplayName = ShortName(borrower),
                MaskedCardNumber = MaskCard(borrower.CardNumber),
                Loans = loanRows,
                UnpaidFines = unpaidFines.Select(f => new KioskFineRow
                {
                    ItemName = f.Loan?.Item?.Name ?? "Late return",
                    DaysLate = f.DaysLate,
                    Amount = f.Amount,
                    CreatedDate = f.CreatedDate
                }).ToList(),
                UnpaidFinesTotal = unpaidFines.Sum(f => f.Amount),
                AccruingFinesTotal = loanRows.Sum(r => r.AccruingFine),
                ReservationsEnabled = _reservations.IsEnabled,
                Reservations = _reservations.IsEnabled
                    ? await _reservations.GetForBorrowerAsync(borrower.Id)
                    : Array.Empty<KioskReservationInfo>(),
                MaxActiveLoans = _options.MaxActiveLoans
            };

            model.BorrowBlockedReason = GetBorrowBlockedReason(loans, model.UnpaidFinesTotal);
            model.CanBorrow = model.BorrowBlockedReason == null;

            return View(model);
        }

        // ------------------------------------------------------------------
        // Catalogue search (works before and after identifying)
        // ------------------------------------------------------------------

        // GET: /Kiosk/Search?q=hobbit&category=Book&availableOnly=true
        [HttpGet]
        public async Task<IActionResult> Search(string? q, string? category = "All", bool availableOnly = false)
        {
            var borrower = await GetCurrentBorrowerAsync();

            var allowedCategories = new[] { "All", "Book", "Music", "Toy" };
            category = allowedCategories.Contains(category) ? category! : "All";

            var model = new KioskSearchViewModel
            {
                Query = q?.Trim(),
                Category = category,
                AvailableOnly = availableOnly,
                IsPatronIdentified = borrower != null,
                HasSearched = !string.IsNullOrWhiteSpace(q) || category != "All" || availableOnly
            };

            if (model.HasSearched)
            {
                var query = _context.Items
                    .Where(i => i.Status != ItemStatus.Destroyed) // Destroyed items are not shown to patrons
                    .ApplyCategory(category)
                    .ApplyKeywordSearch(q);

                if (availableOnly)
                {
                    query = query.Where(i => i.Status == ItemStatus.Available);
                }

                var results = await query
                    .OrderBy(i => i.Status)          // Available first
                    .ThenBy(i => i.Name)
                    .Take(KioskSearchViewModel.MaxResults + 1)
                    .ToListAsync();

                model.ResultsTruncated = results.Count > KioskSearchViewModel.MaxResults;
                model.Results = results.Take(KioskSearchViewModel.MaxResults).ToList();
            }

            return View(model);
        }

        // ------------------------------------------------------------------
        // Checkout
        // ------------------------------------------------------------------

        // GET: /Kiosk/Checkout?code=BOK-1001
        [HttpGet]
        public async Task<IActionResult> Checkout(string? code)
        {
            var borrower = await GetCurrentBorrowerAsync();
            if (borrower == null) return SessionEnded("Please scan your library card to borrow items.");

            var model = new KioskCheckoutViewModel
            {
                LibraryCode = CirculationService.Normalise(code),
                ProposedDueDate = DateTime.UtcNow.AddDays(_options.LoanDays)
            };

            if (string.IsNullOrWhiteSpace(code))
            {
                return View(model); // Show the "scan an item" screen
            }

            model.Item = await _circulation.FindItemByCodeAsync(code);
            if (model.Item == null)
            {
                model.ErrorMessage = $"We couldn't find an item with code '{model.LibraryCode}'. Check the label and try again.";
                return View(model);
            }

            if (model.Item.Status != ItemStatus.Available)
            {
                model.ErrorMessage = UnavailableMessage(model.Item);
                return View(model);
            }

            var loans = await _circulation.GetCurrentLoansAsync(borrower.Id);
            var unpaid = borrower.Fines.Where(f => !f.IsPaid).Sum(f => f.Amount);
            model.ErrorMessage = GetBorrowBlockedReason(loans, unpaid);

            return View(model);
        }

        // POST: /Kiosk/Checkout
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Checkout")]
        public async Task<IActionResult> CheckoutConfirmed(KioskCheckoutViewModel model)
        {
            var borrower = await GetCurrentBorrowerAsync();
            if (borrower == null) return SessionEnded();

            model.ProposedDueDate = DateTime.UtcNow.AddDays(_options.LoanDays);

            if (!ModelState.IsValid)
            {
                return View("Checkout", model);
            }

            var result = await _circulation.CheckoutAsync(new CheckoutRequest
            {
                CardNumber = borrower.CardNumber,
                LibraryCode = model.LibraryCode,
                LoanDays = _options.LoanDays,
                Remarks = $"Self-service kiosk checkout ({_options.BranchName})."
            }, _options.ToPolicy());

            if (!result.Succeeded)
            {
                if (result.Error is CheckoutError.CardNotFound or CheckoutError.BorrowerInactive)
                {
                    return SessionEnded("This library card can no longer be used. Please see the front desk.");
                }

                model.Item = result.Item;
                model.ErrorMessage = result.Error switch
                {
                    CheckoutError.ItemNotFound => $"We couldn't find an item with code '{CirculationService.Normalise(model.LibraryCode)}'. Check the label and try again.",
                    CheckoutError.ItemUnavailable when result.Item != null => UnavailableMessage(result.Item),
                    _ => result.Message
                };
                return View("Checkout", model);
            }

            _logger.LogInformation("Kiosk: borrower {BorrowerId} checked out item {LibraryCode}.", borrower.Id, result.Item!.LibraryCode);
            return RedirectToAction(nameof(Receipt), new { id = result.Loan!.Id });
        }

        // GET: /Kiosk/Receipt/5
        [HttpGet]
        public async Task<IActionResult> Receipt(int id)
        {
            var borrower = await GetCurrentBorrowerAsync();
            if (borrower == null) return SessionEnded();

            var loan = await _context.Loans
                .Include(l => l.Item)
                .FirstOrDefaultAsync(l => l.Id == id && l.BorrowerId == borrower.Id); // Only the current patron's loans

            if (loan == null) return RedirectToAction(nameof(Account));

            var onLoan = await _context.Loans.CountAsync(l => l.BorrowerId == borrower.Id && l.Status != LoanStatus.Returned);

            return View(new KioskReceiptViewModel
            {
                FirstName = borrower.FirstName,
                ItemName = loan.Item?.Name ?? "",
                LibraryCode = loan.Item?.LibraryCode ?? "",
                ItemType = loan.Item?.ItemType ?? "",
                BorrowDate = loan.BorrowDate,
                DueDate = loan.DueDate,
                ItemsOnLoan = onLoan
            });
        }

        // ------------------------------------------------------------------
        // End session / reset
        // ------------------------------------------------------------------

        // POST: /Kiosk/End
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult End(bool timedOut = false)
        {
            ClearPatron();
            TempData["KioskInfo"] = timedOut
                ? "Your session ended because the kiosk was idle. Scan your card to start again."
                : "You're signed out. Thank you for visiting!";
            return RedirectToAction(nameof(Index));
        }

        // ------------------------------------------------------------------
        // Staff helper: printable QR test cards (Reception/Admin only)
        // ------------------------------------------------------------------

        // GET: /Kiosk/TestCards
        [HttpGet]
        [Authorize(Roles = "Reception,Admin")]
        public async Task<IActionResult> TestCards()
        {
            var model = new KioskTestCardsViewModel
            {
                Borrowers = await _context.Borrowers.OrderBy(b => b.CardNumber).ToListAsync(),
                AvailableItems = await _context.Items.Where(i => i.Status == ItemStatus.Available)
                    .OrderBy(i => i.LibraryCode).ToListAsync()
            };
            return View(model);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Returns the patron for this kiosk session, or null if there is none or it has been idle
        /// for longer than the configured timeout (server-side check, independent of the browser timer).
        /// </summary>
        private async Task<Borrower?> GetCurrentBorrowerAsync()
        {
            var borrowerId = HttpContext.Session.GetInt32(BorrowerIdKey);
            if (borrowerId == null) return null;

            var lastActivity = HttpContext.Session.GetString(LastActivityKey);
            var maxIdle = TimeSpan.FromSeconds(_options.IdleTimeoutSeconds + _options.IdleWarningSeconds + 10);
            if (lastActivity == null
                || !long.TryParse(lastActivity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
                || DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc) > maxIdle)
            {
                ClearPatron();
                return null;
            }

            var borrower = await _context.Borrowers
                .Include(b => b.Fines)
                .FirstOrDefaultAsync(b => b.Id == borrowerId.Value);

            if (borrower == null || !borrower.IsActive)
            {
                ClearPatron();
                return null;
            }

            Touch();
            ViewData["KioskPatronName"] = borrower.FirstName;
            return borrower;
        }

        private void Touch() =>
            HttpContext.Session.SetString(LastActivityKey, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));

        private void ClearPatron()
        {
            HttpContext.Session.Remove(BorrowerIdKey);
            HttpContext.Session.Remove(LastActivityKey);
        }

        private IActionResult SessionEnded(string? message = null)
        {
            TempData["KioskInfo"] = message ?? "Your kiosk session has ended. Scan your card to start again.";
            return RedirectToAction(nameof(Index));
        }

        private string? GetBorrowBlockedReason(IReadOnlyCollection<Loan> currentLoans, decimal unpaidFines)
        {
            if (_options.BlockWhenOverdue && currentLoans.Any(l => l.DueDate < DateTime.UtcNow))
                return "You have an overdue item. Please return it before borrowing more.";

            if (currentLoans.Count >= _options.MaxActiveLoans)
                return $"You have reached the self-service limit of {_options.MaxActiveLoans} items on loan.";

            if (unpaidFines > _options.MaxUnpaidFines)
                return $"You have ${unpaidFines:F2} in unpaid fines. Please pay at the front desk before borrowing.";

            return null;
        }

        private static string UnavailableMessage(Item item) => item.Status switch
        {
            ItemStatus.Borrowed => $"'{item.Name}' is already on loan to someone else. Ask staff about placing a hold.",
            ItemStatus.Damaged => $"'{item.Name}' is being repaired and can't be borrowed right now. Ask staff about placing a hold.",
            _ => $"'{item.Name}' is not available to borrow."
        };

        private void RegisterFailedAttempt()
        {
            var attempts = (HttpContext.Session.GetInt32(FailedAttemptsKey) ?? 0) + 1;
            HttpContext.Session.SetInt32(FailedAttemptsKey, attempts);

            if (attempts >= _options.MaxFailedCardAttempts)
            {
                var until = DateTime.UtcNow.AddSeconds(_options.LockoutSeconds);
                HttpContext.Session.SetString(LockedUntilKey, until.Ticks.ToString(CultureInfo.InvariantCulture));
                HttpContext.Session.Remove(FailedAttemptsKey);
                _logger.LogWarning("Kiosk: too many failed card attempts; lookups paused for {Seconds}s.", _options.LockoutSeconds);
            }
        }

        private void ApplyLockoutState(KioskStartViewModel model)
        {
            var value = HttpContext.Session.GetString(LockedUntilKey);
            if (value == null || !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)) return;

            var remaining = new DateTime(ticks, DateTimeKind.Utc) - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                HttpContext.Session.Remove(LockedUntilKey);
                return;
            }

            model.IsLockedOut = true;
            model.LockoutSecondsRemaining = (int)Math.Ceiling(remaining.TotalSeconds);
            model.ErrorMessage = "Too many unsuccessful attempts. Please wait a moment or ask at the front desk.";
        }

        private static string ShortName(Borrower b) =>
            string.IsNullOrWhiteSpace(b.LastName) ? b.FirstName : $"{b.FirstName} {b.LastName[0]}.";

        /// <summary>Shows only the last 4 characters of a card number, e.g. "•••• 1001".</summary>
        internal static string MaskCard(string card) =>
            card.Length <= 4 ? card : $"•••• {card[^4..]}";
    }
}

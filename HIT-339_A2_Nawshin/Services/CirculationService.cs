using Microsoft.EntityFrameworkCore;
using Bibliotheca.Data;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.Enums;

namespace Bibliotheca.Services
{
    /// <summary>
    /// Shared borrowing (circulation) rules used by both the Reception desk
    /// (LoansController) and the Public Self-Service Kiosk (KioskController).
    /// Keeping the rules here means the kiosk does not duplicate desk logic.
    /// </summary>
    public interface ICirculationService
    {
        /// <summary>Daily late fine used across the system ($1.00 per day).</summary>
        decimal DailyFineRate { get; }

        Task<Borrower?> FindBorrowerByCardAsync(string? cardNumber);

        Task<Item?> FindItemByCodeAsync(string? libraryCode);

        Task<CheckoutResult> CheckoutAsync(CheckoutRequest request, CheckoutPolicy? policy = null);

        Task<List<Loan>> GetCurrentLoansAsync(int borrowerId);

        Task<List<Fine>> GetUnpaidFinesAsync(int borrowerId);

        /// <summary>Fine that would be charged if the loan were returned at <paramref name="asOf"/>.</summary>
        decimal CalculateFine(DateTime dueDate, DateTime asOf, out int daysLate);
    }

    public sealed class CheckoutRequest
    {
        public string CardNumber { get; set; } = string.Empty;
        public string LibraryCode { get; set; } = string.Empty;
        public int LoanDays { get; set; } = 14;
        public string? Remarks { get; set; }
    }

    /// <summary>
    /// Optional extra rules. The Reception desk passes no policy (staff judgement applies);
    /// the kiosk passes the limits configured in appsettings.json ("Kiosk" section).
    /// </summary>
    public sealed class CheckoutPolicy
    {
        public int? MaxActiveLoans { get; set; }
        public bool BlockWhenOverdue { get; set; }
        public decimal? MaxUnpaidFines { get; set; }
    }

    public enum CheckoutError
    {
        None,
        InvalidLoanDays,
        CardNotFound,
        BorrowerInactive,
        ItemNotFound,
        ItemUnavailable,
        LoanLimitReached,
        HasOverdueItems,
        UnpaidFinesTooHigh
    }

    public sealed class CheckoutResult
    {
        public CheckoutError Error { get; init; }
        public bool Succeeded => Error == CheckoutError.None;
        public string Message { get; init; } = string.Empty;
        public Borrower? Borrower { get; init; }
        public Item? Item { get; init; }
        public Loan? Loan { get; init; }

        /// <summary>True when the error relates to the card/borrower rather than the item.</summary>
        public bool IsBorrowerError => Error is CheckoutError.CardNotFound or CheckoutError.BorrowerInactive
            or CheckoutError.LoanLimitReached or CheckoutError.HasOverdueItems or CheckoutError.UnpaidFinesTooHigh;

        internal static CheckoutResult Fail(CheckoutError error, string message, Borrower? borrower = null, Item? item = null)
            => new() { Error = error, Message = message, Borrower = borrower, Item = item };
    }

    public class CirculationService : ICirculationService
    {
        private readonly ApplicationDbContext _context;

        public CirculationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public decimal DailyFineRate => 1.00m;

        public static string Normalise(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

        public async Task<Borrower?> FindBorrowerByCardAsync(string? cardNumber)
        {
            var card = Normalise(cardNumber);
            if (card.Length == 0) return null;

            return await _context.Borrowers
                .Include(b => b.Fines)
                .FirstOrDefaultAsync(b => b.CardNumber.ToUpper() == card);
        }

        public async Task<Item?> FindItemByCodeAsync(string? libraryCode)
        {
            var code = Normalise(libraryCode);
            if (code.Length == 0) return null;

            return await _context.Items.FirstOrDefaultAsync(i => i.LibraryCode.ToUpper() == code);
        }

        public async Task<CheckoutResult> CheckoutAsync(CheckoutRequest request, CheckoutPolicy? policy = null)
        {
            if (request.LoanDays < 1 || request.LoanDays > 60)
            {
                return CheckoutResult.Fail(CheckoutError.InvalidLoanDays, "Loan duration must be between 1 and 60 days.");
            }

            // 1. Verify borrower
            var borrower = await FindBorrowerByCardAsync(request.CardNumber);
            if (borrower == null)
            {
                return CheckoutResult.Fail(CheckoutError.CardNotFound,
                    $"No borrower found with card number '{request.CardNumber}'.");
            }

            if (!borrower.IsActive)
            {
                return CheckoutResult.Fail(CheckoutError.BorrowerInactive,
                    $"Borrower account '{borrower.FullName}' is inactive/suspended.", borrower);
            }

            // 2. Optional self-service policy checks
            if (policy != null)
            {
                var now = DateTime.UtcNow;
                var currentLoans = await GetCurrentLoansAsync(borrower.Id);

                if (policy.BlockWhenOverdue && currentLoans.Any(l => l.DueDate < now))
                {
                    return CheckoutResult.Fail(CheckoutError.HasOverdueItems,
                        "You have an overdue item. Please return it before borrowing more.", borrower);
                }

                if (policy.MaxActiveLoans.HasValue && currentLoans.Count >= policy.MaxActiveLoans.Value)
                {
                    return CheckoutResult.Fail(CheckoutError.LoanLimitReached,
                        $"You have reached the self-service limit of {policy.MaxActiveLoans.Value} items on loan.", borrower);
                }

                if (policy.MaxUnpaidFines.HasValue)
                {
                    var unpaid = borrower.Fines.Where(f => !f.IsPaid).Sum(f => f.Amount);
                    if (unpaid > policy.MaxUnpaidFines.Value)
                    {
                        return CheckoutResult.Fail(CheckoutError.UnpaidFinesTooHigh,
                            $"You have ${unpaid:F2} in unpaid fines. Please pay at the front desk before borrowing.", borrower);
                    }
                }
            }

            // 3. Verify item
            var item = await FindItemByCodeAsync(request.LibraryCode);
            if (item == null)
            {
                return CheckoutResult.Fail(CheckoutError.ItemNotFound,
                    $"No library item found with code '{request.LibraryCode}'.", borrower);
            }

            if (item.Status != ItemStatus.Available)
            {
                return CheckoutResult.Fail(CheckoutError.ItemUnavailable,
                    $"Item '{item.Name}' is not available (Current Status: {item.Status}).", borrower, item);
            }

            // 4. Create loan record and mark item as borrowed
            var borrowDate = DateTime.UtcNow;
            var loan = new Loan
            {
                ItemId = item.Id,
                BorrowerId = borrower.Id,
                BorrowDate = borrowDate,
                DueDate = borrowDate.AddDays(request.LoanDays),
                Status = LoanStatus.Active,
                Remarks = request.Remarks
            };

            item.Status = ItemStatus.Borrowed;
            _context.Loans.Add(loan);
            await _context.SaveChangesAsync();

            return new CheckoutResult
            {
                Error = CheckoutError.None,
                Message = $"Successfully issued '{item.Name}' ({item.LibraryCode}) to {borrower.FullName} (Due: {loan.DueDate:d MMM yyyy}).",
                Borrower = borrower,
                Item = item,
                Loan = loan
            };
        }

        public async Task<List<Loan>> GetCurrentLoansAsync(int borrowerId)
        {
            return await _context.Loans
                .Include(l => l.Item)
                .Where(l => l.BorrowerId == borrowerId && l.Status != LoanStatus.Returned)
                .OrderBy(l => l.DueDate)
                .ToListAsync();
        }

        public async Task<List<Fine>> GetUnpaidFinesAsync(int borrowerId)
        {
            return await _context.Fines
                .Include(f => f.Loan)
                    .ThenInclude(l => l!.Item)
                .Where(f => f.BorrowerId == borrowerId && !f.IsPaid)
                .OrderByDescending(f => f.CreatedDate)
                .ToListAsync();
        }

        public decimal CalculateFine(DateTime dueDate, DateTime asOf, out int daysLate)
        {
            daysLate = 0;
            if (asOf <= dueDate) return 0m;

            daysLate = (int)Math.Ceiling((asOf - dueDate).TotalDays);
            if (daysLate < 1) daysLate = 1;
            return daysLate * DailyFineRate;
        }
    }
}

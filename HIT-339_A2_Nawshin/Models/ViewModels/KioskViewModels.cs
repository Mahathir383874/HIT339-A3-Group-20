using System.ComponentModel.DataAnnotations;
using Bibliotheca.Models.Entities;
using Bibliotheca.Services;

namespace Bibliotheca.Models.ViewModels
{
    /// <summary>Start screen: patron scans or types their library card number.</summary>
    public class KioskStartViewModel
    {
        [Required(ErrorMessage = "Please scan or type your library card number.")]
        [StringLength(30, ErrorMessage = "That card number is too long.")]
        [Display(Name = "Library card number")]
        public string CardNumber { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
        public bool IsLockedOut { get; set; }
        public int LockoutSecondsRemaining { get; set; }
    }

    public class KioskLoanRow
    {
        public int LoanId { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public string LibraryCode { get; init; } = string.Empty;
        public string ItemType { get; init; } = string.Empty;
        public string? ImageUrl { get; init; }
        public DateTime BorrowDate { get; init; }
        public DateTime DueDate { get; init; }
        public bool IsOverdue { get; init; }
        public int DaysOverdue { get; init; }
        public int DaysUntilDue { get; init; }
        public decimal AccruingFine { get; init; }
    }

    public class KioskFineRow
    {
        public string ItemName { get; init; } = string.Empty;
        public int DaysLate { get; init; }
        public decimal Amount { get; init; }
        public DateTime CreatedDate { get; init; }
    }

    /// <summary>Account summary shown after a successful card lookup.</summary>
    public class KioskAccountViewModel
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string MaskedCardNumber { get; set; } = string.Empty;

        public List<KioskLoanRow> Loans { get; set; } = new();
        public List<KioskFineRow> UnpaidFines { get; set; } = new();
        public decimal UnpaidFinesTotal { get; set; }
        public decimal AccruingFinesTotal { get; set; }

        public bool ReservationsEnabled { get; set; }
        public IReadOnlyList<KioskReservationInfo> Reservations { get; set; } = Array.Empty<KioskReservationInfo>();

        public bool CanBorrow { get; set; }
        public string? BorrowBlockedReason { get; set; }
        public int MaxActiveLoans { get; set; }
    }

    public class KioskSearchViewModel
    {
        public string? Query { get; set; }
        public string Category { get; set; } = "All";
        public bool AvailableOnly { get; set; }
        public bool HasSearched { get; set; }
        public bool IsPatronIdentified { get; set; }
        public List<Item> Results { get; set; } = new();
        public const int MaxResults = 30;
        public bool ResultsTruncated { get; set; }
    }

    /// <summary>Confirm-checkout screen: shows the scanned item before the patron confirms.</summary>
    public class KioskCheckoutViewModel
    {
        [Required(ErrorMessage = "Please scan or type the item's library code.")]
        [StringLength(20)]
        [Display(Name = "Item library code")]
        public string LibraryCode { get; set; } = string.Empty;

        public Item? Item { get; set; }
        public DateTime ProposedDueDate { get; set; }
        public string? ErrorMessage { get; set; }
        public bool CanConfirm => Item != null && ErrorMessage == null;
    }

    public class KioskReceiptViewModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string LibraryCode { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public DateTime BorrowDate { get; set; }
        public DateTime DueDate { get; set; }
        public int ItemsOnLoan { get; set; }
    }

    /// <summary>Staff-only page that prints QR test cards for kiosk testing.</summary>
    public class KioskTestCardsViewModel
    {
        public List<Borrower> Borrowers { get; set; } = new();
        public List<Item> AvailableItems { get; set; } = new();
    }
}

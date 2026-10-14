using System.ComponentModel.DataAnnotations;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.Enums;

namespace Bibliotheca.Models.ViewModels
{
    public class IssueLoanViewModel
    {
        [Required(ErrorMessage = "Item Library Code is required")]
        [Display(Name = "Item Library Code")]
        public string LibraryCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Borrower Card Number is required")]
        [Display(Name = "Borrower Card Number")]
        public string CardNumber { get; set; } = string.Empty;

        [Range(1, 60, ErrorMessage = "Loan duration must be between 1 and 60 days")]
        [Display(Name = "Loan Duration (Days)")]
        public int LoanDays { get; set; } = 14;

        [Display(Name = "Issue Notes / Remarks")]
        [StringLength(250)]
        public string? Remarks { get; set; }

        // Optional helpers for view dropdowns
        public List<Item>? AvailableItems { get; set; }
        public List<Borrower>? ActiveBorrowers { get; set; }
    }

    public class ProcessReturnViewModel
    {
        [Required]
        public int LoanId { get; set; }

        public string ItemName { get; set; } = string.Empty;
        public string LibraryCode { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string BorrowerName { get; set; } = string.Empty;
        public string CardNumber { get; set; } = string.Empty;
        public DateTime BorrowDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

        public int DaysLate { get; set; }
        public decimal CalculatedFine { get; set; }

        [Display(Name = "Condition upon Return")]
        public ItemStatus NewItemStatus { get; set; } = ItemStatus.Available;

        [Display(Name = "Collect Fine Immediately")]
        public bool PayFineImmediately { get; set; } = false;

        [Display(Name = "Return Remarks")]
        [StringLength(250)]
        public string? Remarks { get; set; }

        // ---- Branch-aware returns (reception desks) ----
        [Display(Name = "Returned to Branch")]
        public int? ReturnBranchId { get; set; }

        public string? ItemBranchName { get; set; }
        public string? DeskBranchName { get; set; }

        /// <summary>Patrons waiting in the hold queue for this item.</summary>
        public int WaitingCount { get; set; }

        public List<Branch> Branches { get; set; } = new();
    }

    public class PayFineViewModel
    {
        [Required]
        public int FineId { get; set; }
        public decimal Amount { get; set; }
        public string BorrowerName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int DaysLate { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bibliotheca.Models.Enums;

namespace Bibliotheca.Models.Entities
{
    /// <summary>
    /// Loan transaction record representing an item borrowed by a patron.
    /// Handled by Reception staff.
    /// </summary>
    public class Loan
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Library Item")]
        public int ItemId { get; set; }

        [ForeignKey(nameof(ItemId))]
        public virtual Item? Item { get; set; }

        [Required]
        [Display(Name = "Borrower")]
        public int BorrowerId { get; set; }

        [ForeignKey(nameof(BorrowerId))]
        public virtual Borrower? Borrower { get; set; }

        [Required]
        [Display(Name = "Borrow Date")]
        public DateTime BorrowDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(14); // 14-day default loan period

        [Display(Name = "Return Date")]
        public DateTime? ReturnDate { get; set; }

        [Required]
        [Display(Name = "Loan Status")]
        public LoanStatus Status { get; set; } = LoanStatus.Active;

        [StringLength(250)]
        [Display(Name = "Notes / Remarks")]
        public string? Remarks { get; set; }

        // Navigation property for late fine (if incurred)
        public virtual Fine? Fine { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bibliotheca.Models.Entities
{
    /// <summary>
    /// Fine entity generated when an item is returned after its due date.
    /// Standard calculation: $1.00 per late day.
    /// </summary>
    public class Fine
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int LoanId { get; set; }

        [ForeignKey(nameof(LoanId))]
        public virtual Loan? Loan { get; set; }

        [Required]
        public int BorrowerId { get; set; }

        [ForeignKey(nameof(BorrowerId))]
        public virtual Borrower? Borrower { get; set; }

        [Required]
        [Range(0, 1000)]
        [DataType(DataType.Currency)]
        [Display(Name = "Fine Amount ($)")]
        public decimal Amount { get; set; }

        [Display(Name = "Days Overdue")]
        public int DaysLate { get; set; }

        [StringLength(200)]
        [Display(Name = "Calculation Details")]
        public string? CalculationDetails { get; set; }

        [Display(Name = "Paid Status")]
        public bool IsPaid { get; set; } = false;

        [Display(Name = "Payment Date")]
        public DateTime? PaidDate { get; set; }

        [Display(Name = "Date Incurred")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}

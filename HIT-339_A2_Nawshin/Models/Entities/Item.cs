using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bibliotheca.Models.Enums;

namespace Bibliotheca.Models.Entities
{
    /// <summary>
    /// Base Item Superclass for all library catalog items.
    /// Inherited by Book, Music, and Toy derived classes using Table-Per-Hierarchy (TPH) inheritance.
    /// </summary>
    public abstract class Item
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Item title/name is required")]
        [StringLength(120, ErrorMessage = "Name cannot exceed 120 characters")]
        [Display(Name = "Item Title / Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Library code is required")]
        [RegularExpression(@"^[A-Z]{3}-\d{4}$", ErrorMessage = "Library code must be in the standard format (e.g., BOK-1001, MUS-2001, TOY-3001)")]
        [Display(Name = "Library Code")]
        public string LibraryCode { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Status")]
        public ItemStatus Status { get; set; } = ItemStatus.Available;

        [Display(Name = "Category")]
        public string ItemType { get; set; } = string.Empty;

        [Display(Name = "Image / Graphic")]
        public string? ImageUrl { get; set; }

        [Range(0, 10000, ErrorMessage = "Replacement cost must be a positive value")]
        [DataType(DataType.Currency)]
        [Display(Name = "Replacement Cost ($)")]
        public decimal ReplacementCost { get; set; } = 25.00m;

        [Display(Name = "Date Added")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>Branch that currently holds the item (updated when an inter-branch transfer completes).</summary>
        [Display(Name = "Branch")]
        public int? BranchId { get; set; }

        [ForeignKey(nameof(BranchId))]
        public virtual Branch? Branch { get; set; }

        // Navigation property for loan history
        public virtual ICollection<Loan> Loans { get; set; } = new List<Loan>();

        // Holds placed on this item and its inter-branch transfer history
        public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public virtual ICollection<BranchTransfer> Transfers { get; set; } = new List<BranchTransfer>();
    }
}

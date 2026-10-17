using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bibliotheca.Models.Enums;

namespace Bibliotheca.Models.Entities
{
    /// <summary>
    /// A hold placed by a patron on an item that cannot be borrowed right now.
    /// Queue order is first-come-first-served: the lower the Id, the earlier the hold was placed.
    /// Queue positions are always calculated from the active holds, so they stay correct after a cancellation.
    /// </summary>
    public class Reservation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ItemId { get; set; }

        [ForeignKey(nameof(ItemId))]
        public virtual Item? Item { get; set; }

        [Required]
        public int BorrowerId { get; set; }

        [ForeignKey(nameof(BorrowerId))]
        public virtual Borrower? Borrower { get; set; }

        [Display(Name = "Hold Status")]
        public ReservationStatus Status { get; set; } = ReservationStatus.Waiting;

        [Display(Name = "Placed")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Ready Since")]
        public DateTime? ReadyDate { get; set; }

        public DateTime? FulfilledDate { get; set; }

        public DateTime? CancelledDate { get; set; }

        /// <summary>Branch where the item is waiting for the patron (set when the hold becomes Ready).</summary>
        public int? PickupBranchId { get; set; }

        [ForeignKey(nameof(PickupBranchId))]
        public virtual Branch? PickupBranch { get; set; }

        [StringLength(40)]
        [Display(Name = "Placed Via")]
        public string? PlacedVia { get; set; }

        [StringLength(200)]
        public string? CancelReason { get; set; }
    }
}

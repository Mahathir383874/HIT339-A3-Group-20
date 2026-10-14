using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bibliotheca.Models.Enums;

namespace Bibliotheca.Models.Entities
{
    /// <summary>
    /// Movement of one item from one branch to another. Rows are never deleted, so the table
    /// doubles as the transfer history / audit trail (who requested, dispatched, received or cancelled).
    /// </summary>
    public class BranchTransfer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ItemId { get; set; }

        [ForeignKey(nameof(ItemId))]
        public virtual Item? Item { get; set; }

        [Required]
        public int FromBranchId { get; set; }

        [ForeignKey(nameof(FromBranchId))]
        public virtual Branch? FromBranch { get; set; }

        [Required]
        public int ToBranchId { get; set; }

        [ForeignKey(nameof(ToBranchId))]
        public virtual Branch? ToBranch { get; set; }

        public TransferStatus Status { get; set; } = TransferStatus.Requested;

        public DateTime RequestedDate { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? RequestedBy { get; set; }

        public DateTime? DispatchedDate { get; set; }

        [StringLength(100)]
        public string? DispatchedBy { get; set; }

        public DateTime? CompletedDate { get; set; }

        [StringLength(100)]
        public string? CompletedBy { get; set; }

        public DateTime? CancelledDate { get; set; }

        [StringLength(100)]
        public string? CancelledBy { get; set; }

        [StringLength(250)]
        public string? Notes { get; set; }

        [StringLength(250)]
        public string? CancelReason { get; set; }
    }
}

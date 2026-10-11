using System.ComponentModel.DataAnnotations;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.Enums;

namespace Bibliotheca.Models.ViewModels
{
    public class BranchSummaryRow
    {
        public Branch Branch { get; init; } = new Branch();
        public int Total { get; init; }
        public int Available { get; init; }
        public int Borrowed { get; init; }
        public int Damaged { get; init; }
        public int Destroyed { get; init; }
        public int IncomingTransfers { get; init; }
        public int OutgoingTransfers { get; init; }
    }

    public class BranchInventoryViewModel
    {
        public int? BranchId { get; set; }
        public string? Category { get; set; } = "All";
        public ItemStatus? Status { get; set; }
        public string? SearchTerm { get; set; }

        public List<Branch> Branches { get; set; } = new();
        public List<Item> Items { get; set; } = new();

        /// <summary>Item id -> hold queue / transfer state.</summary>
        public Dictionary<int, ItemLendingState> LendingStates { get; set; } = new();
    }

    public class TransferListViewModel
    {
        public string Filter { get; set; } = "Active";
        public int? BranchId { get; set; }
        public List<Branch> Branches { get; set; } = new();
        public List<BranchTransfer> Transfers { get; set; } = new();
    }

    public class TransferCreateViewModel
    {
        [Required(ErrorMessage = "Please choose the item to transfer")]
        [Display(Name = "Item")]
        public int? ItemId { get; set; }

        [Required(ErrorMessage = "Please choose the destination branch")]
        [Display(Name = "Destination Branch")]
        public int? ToBranchId { get; set; }

        [StringLength(250, ErrorMessage = "Notes cannot exceed 250 characters")]
        [Display(Name = "Notes (optional)")]
        public string? Notes { get; set; }

        public List<Item> Items { get; set; } = new();
        public List<Branch> Branches { get; set; } = new();
    }

    public class NotificationListViewModel
    {
        public NotificationType? Type { get; set; }
        public string? SearchTerm { get; set; }
        public List<Notification> Rows { get; set; } = new();
        public int TotalCount { get; set; }
        public Dictionary<NotificationType, int> CountsByType { get; set; } = new();
        public const int MaxRows = 200;
    }
}

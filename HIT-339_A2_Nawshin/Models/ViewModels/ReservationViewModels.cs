using System.ComponentModel.DataAnnotations;
using Bibliotheca.Models.Enums;

namespace Bibliotheca.Models.ViewModels
{
    /// <summary>One hold as shown to staff or to the patron, including its live queue position.</summary>
    public class ReservationRow
    {
        public int Id { get; init; }
        public int ItemId { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public string LibraryCode { get; init; } = string.Empty;
        public string ItemType { get; init; } = string.Empty;
        public ItemStatus ItemStatus { get; init; }
        public string? CurrentBranch { get; init; }
        public int BorrowerId { get; init; }
        public string BorrowerName { get; init; } = string.Empty;
        public string CardNumber { get; init; } = string.Empty;
        public ReservationStatus Status { get; init; }

        /// <summary>1 = first in the queue. Null once the hold is no longer active.</summary>
        public int? QueuePosition { get; init; }

        public DateTime CreatedDate { get; init; }
        public DateTime? ReadyDate { get; init; }
        public DateTime? FulfilledDate { get; init; }
        public DateTime? CancelledDate { get; init; }
        public string? PickupBranch { get; init; }
        public string? PlacedVia { get; init; }
        public string? CancelReason { get; init; }

        public bool IsActive => Status == ReservationStatus.Waiting || Status == ReservationStatus.Ready;
    }

    /// <summary>What stands between a patron and borrowing an item that is marked Available.</summary>
    public class ItemLendingState
    {
        public int ItemId { get; init; }

        /// <summary>True while a transfer to another branch is requested or in progress.</summary>
        public bool InTransfer { get; init; }
        public string? TransferDestination { get; init; }

        /// <summary>Number of active holds (Waiting + Ready).</summary>
        public int QueueLength { get; init; }

        public int? FirstInQueueBorrowerId { get; init; }

        /// <summary>True when the first hold has been promoted to Ready (the item is on the hold shelf).</summary>
        public bool OnHoldShelf { get; init; }

        /// <summary>Same rule the circulation service applies at checkout.</summary>
        public bool IsBorrowableBy(int? borrowerId)
        {
            if (InTransfer) return false;
            if (QueueLength == 0) return true;
            return borrowerId.HasValue && FirstInQueueBorrowerId == borrowerId.Value;
        }
    }

    public class ReservationListViewModel
    {
        public string Filter { get; set; } = "Active";
        public string? SearchTerm { get; set; }
        public List<ReservationRow> Rows { get; set; } = new();
    }

    public class PlaceHoldViewModel
    {
        [Required(ErrorMessage = "Borrower card number is required")]
        [Display(Name = "Borrower Card Number")]
        public string CardNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Item library code is required")]
        [Display(Name = "Item Library Code")]
        public string LibraryCode { get; set; } = string.Empty;

        /// <summary>Borrowed / damaged / otherwise unavailable items to pick from.</summary>
        public List<Bibliotheca.Models.Entities.Item> HoldableItems { get; set; } = new();
        public List<Bibliotheca.Models.Entities.Borrower> ActiveBorrowers { get; set; } = new();
    }
}

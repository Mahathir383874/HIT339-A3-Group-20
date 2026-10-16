using Bibliotheca.Models.Entities;

namespace Bibliotheca.Models.ViewModels
{
    public class ManagerDashboardViewModel
    {
        // 1. Item Status Statistics
        public int TotalItems { get; set; }
        public int AvailableItemsCount { get; set; }
        public int BorrowedItemsCount { get; set; }
        public int DamagedItemsCount { get; set; }
        public int DestroyedItemsCount { get; set; }

        public int TotalBooksCount { get; set; }
        public int TotalMusicCount { get; set; }
        public int TotalToysCount { get; set; }

        public List<Item> DamagedOrDestroyedItems { get; set; } = new List<Item>();
        public decimal TotalDamagedReplacementCost { get; set; }

        // 2. Borrowing Statistics
        public int TotalLoansCount { get; set; }
        public int ActiveLoansCount { get; set; }
        public int ReturnedLoansCount { get; set; }
        public int OverdueLoansCount { get; set; }
        public int BorrowsThisWeek { get; set; }
        public int BorrowsThisMonth { get; set; }
        public int BorrowsThisYear { get; set; }

        public List<MostBorrowedItemStat> MostBorrowedItems { get; set; } = new List<MostBorrowedItemStat>();
        public List<CategoryBorrowStat> CategoryBorrowStats { get; set; } = new List<CategoryBorrowStat>();

        // 3. Fine & Revenue Statistics
        public decimal TotalFinesGenerated { get; set; }
        public decimal TotalFinesCollected { get; set; }
        public decimal TotalFinesOutstanding { get; set; }
        public int TotalFineRecordsCount { get; set; }
        public int UnpaidFineCount { get; set; }
        public List<Fine> RecentFines { get; set; } = new List<Fine>();

        // Financial recovery comparison
        public decimal NetFineVsDamageBalance => TotalFinesCollected - TotalDamagedReplacementCost;
    }

    public class MostBorrowedItemStat
    {
        public int ItemId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string LibraryCode { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public int BorrowCount { get; set; }
    }

    public class CategoryBorrowStat
    {
        public string Category { get; set; } = string.Empty;
        public int BorrowCount { get; set; }
    }
}

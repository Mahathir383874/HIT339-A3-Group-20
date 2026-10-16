using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bibliotheca.Data;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.Enums;
using Bibliotheca.Models.ViewModels;

namespace Bibliotheca.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    public class ManagerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ManagerController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Manager
        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;
            var sevenDaysAgo = now.AddDays(-7);
            var thirtyDaysAgo = now.AddDays(-30);
            var oneYearAgo = now.AddDays(-365);

            // 1. Items & Inventory Status Stats
            var allItems = await _context.Items.ToListAsync();
            var totalItems = allItems.Count;
            var availableItemsCount = allItems.Count(i => i.Status == ItemStatus.Available);
            var borrowedItemsCount = allItems.Count(i => i.Status == ItemStatus.Borrowed);
            var damagedItemsCount = allItems.Count(i => i.Status == ItemStatus.Damaged);
            var destroyedItemsCount = allItems.Count(i => i.Status == ItemStatus.Destroyed);

            var totalBooksCount = allItems.Count(i => i is Book);
            var totalMusicCount = allItems.Count(i => i is Music);
            var totalToysCount = allItems.Count(i => i is Toy);

            var damagedOrDestroyedItems = allItems
                .Where(i => i.Status == ItemStatus.Damaged || i.Status == ItemStatus.Destroyed)
                .ToList();

            var totalDamagedReplacementCost = damagedOrDestroyedItems.Sum(i => i.ReplacementCost);

            // 2. Borrowing Activity Stats
            var allLoans = await _context.Loans
                .Include(l => l.Item)
                .Include(l => l.Borrower)
                .ToListAsync();

            var totalLoansCount = allLoans.Count;
            var activeLoansCount = allLoans.Count(l => l.Status == LoanStatus.Active);
            var returnedLoansCount = allLoans.Count(l => l.Status == LoanStatus.Returned);
            var overdueLoansCount = allLoans.Count(l => l.Status == LoanStatus.Overdue || (l.Status == LoanStatus.Active && l.DueDate < now));

            var borrowsThisWeek = allLoans.Count(l => l.BorrowDate >= sevenDaysAgo);
            var borrowsThisMonth = allLoans.Count(l => l.BorrowDate >= thirtyDaysAgo);
            var borrowsThisYear = allLoans.Count(l => l.BorrowDate >= oneYearAgo);

            // Top most borrowed items
            var mostBorrowedItems = allLoans
                .Where(l => l.Item != null)
                .GroupBy(l => new { l.ItemId, l.Item!.Name, l.Item.LibraryCode, l.Item.ItemType })
                .Select(g => new MostBorrowedItemStat
                {
                    ItemId = g.Key.ItemId,
                    Title = g.Key.Name,
                    LibraryCode = g.Key.LibraryCode,
                    ItemType = g.Key.ItemType,
                    BorrowCount = g.Count()
                })
                .OrderByDescending(s => s.BorrowCount)
                .Take(5)
                .ToList();

            // Category Borrow Stats
            var categoryBorrowStats = allLoans
                .Where(l => l.Item != null)
                .GroupBy(l => l.Item!.ItemType)
                .Select(g => new CategoryBorrowStat
                {
                    Category = string.IsNullOrEmpty(g.Key) ? "General" : g.Key,
                    BorrowCount = g.Count()
                })
                .OrderByDescending(g => g.BorrowCount)
                .ToList();

            // 3. Fines & Revenue Stats
            var allFines = await _context.Fines
                .Include(f => f.Borrower)
                .Include(f => f.Loan)
                    .ThenInclude(l => l!.Item)
                .ToListAsync();

            var totalFinesGenerated = allFines.Sum(f => f.Amount);
            var totalFinesCollected = allFines.Where(f => f.IsPaid).Sum(f => f.Amount);
            var totalFinesOutstanding = allFines.Where(f => !f.IsPaid).Sum(f => f.Amount);
            var unpaidFineCount = allFines.Count(f => !f.IsPaid);
            var recentFines = allFines.OrderByDescending(f => f.CreatedDate).Take(8).ToList();

            var viewModel = new ManagerDashboardViewModel
            {
                TotalItems = totalItems,
                AvailableItemsCount = availableItemsCount,
                BorrowedItemsCount = borrowedItemsCount,
                DamagedItemsCount = damagedItemsCount,
                DestroyedItemsCount = destroyedItemsCount,
                TotalBooksCount = totalBooksCount,
                TotalMusicCount = totalMusicCount,
                TotalToysCount = totalToysCount,
                DamagedOrDestroyedItems = damagedOrDestroyedItems,
                TotalDamagedReplacementCost = totalDamagedReplacementCost,

                TotalLoansCount = totalLoansCount,
                ActiveLoansCount = activeLoansCount,
                ReturnedLoansCount = returnedLoansCount,
                OverdueLoansCount = overdueLoansCount,
                BorrowsThisWeek = borrowsThisWeek,
                BorrowsThisMonth = borrowsThisMonth,
                BorrowsThisYear = borrowsThisYear,
                MostBorrowedItems = mostBorrowedItems,
                CategoryBorrowStats = categoryBorrowStats,

                TotalFinesGenerated = totalFinesGenerated,
                TotalFinesCollected = totalFinesCollected,
                TotalFinesOutstanding = totalFinesOutstanding,
                TotalFineRecordsCount = allFines.Count,
                UnpaidFineCount = unpaidFineCount,
                RecentFines = recentFines
            };

            return View(viewModel);
        }
    }
}

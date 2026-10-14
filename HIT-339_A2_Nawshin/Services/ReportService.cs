using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Bibliotheca.Data;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.Enums;
using Bibliotheca.Models.ViewModels;

namespace Bibliotheca.Services
{
    public enum ReportKind
    {
        Borrowing,
        Fines,
        Inventory
    }

    /// <summary>
    /// Builds the manager reports as plain tables of text (see ReportDocument) so that the
    /// on-screen view, the CSV export and the PDF export always show exactly the same figures.
    ///
    /// Branch filtering: every BuildXxxAsync method takes an optional branch id. Loans, fines and items are
    /// filtered on the branch that currently holds the item before any figure is calculated, so the
    /// on-screen view, CSV and PDF stay consistent.
    /// </summary>
    public class ReportService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<ReportDocument> BuildAsync(ReportKind kind, DateTime? from, DateTime? to, int? branchId = null)
        {
            return kind switch
            {
                ReportKind.Borrowing => BuildBorrowingAsync(from, to, branchId),
                ReportKind.Fines => BuildFinesAsync(from, to, branchId),
                ReportKind.Inventory => BuildInventoryAsync(branchId),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        // =====================================================================
        // 1. Borrowing statistics
        // =====================================================================
        public async Task<ReportDocument> BuildBorrowingAsync(DateTime? from, DateTime? to, int? branchId = null)
        {
            var (start, endExclusive) = NormaliseRange(from, to);
            var branchName = await BranchNameAsync(branchId);

            var allLoans = await _context.Loans
                .AsNoTracking()
                .Include(l => l.Item)
                .Include(l => l.Borrower)
                .ToListAsync();

            var loans = allLoans
                .Where(l => (!start.HasValue || l.BorrowDate >= start.Value) &&
                            (!endExclusive.HasValue || l.BorrowDate < endExclusive.Value) &&
                            (!branchId.HasValue || l.Item?.BranchId == branchId.Value))
                .ToList();

            var now = DateTime.UtcNow;
            bool IsOverdue(Loan l) => l.Status == LoanStatus.Overdue || (l.Status == LoanStatus.Active && l.DueDate < now);

            var returned = loans.Count(l => l.Status == LoanStatus.Returned);
            var overdue = loans.Count(IsOverdue);
            var stillOut = loans.Count(l => l.Status != LoanStatus.Returned);
            var uniqueBorrowers = loans.Select(l => l.BorrowerId).Distinct().Count();

            var returnedWithDate = loans.Where(l => l.ReturnDate.HasValue).ToList();
            var averageLoanDays = returnedWithDate.Count == 0
                ? "n/a"
                : returnedWithDate.Average(l => (l.ReturnDate!.Value - l.BorrowDate).TotalDays).ToString("0.0", Inv) + " days";

            var document = new ReportDocument
            {
                Title = "Borrowing Statistics Report",
                Subtitle = "Period: " + PeriodText(from, to) + BranchText(branchName),
                GeneratedUtc = now
            };

            document.Summary.Add(new ReportMetric("Loans in period", loans.Count.ToString(Inv)));
            document.Summary.Add(new ReportMetric("Returned", returned.ToString(Inv)));
            document.Summary.Add(new ReportMetric("Still out (not returned)", stillOut.ToString(Inv)));
            document.Summary.Add(new ReportMetric("Overdue right now", overdue.ToString(Inv)));
            document.Summary.Add(new ReportMetric("Return rate", Percent(returned, loans.Count)));
            document.Summary.Add(new ReportMetric("Average loan length (returned items)", averageLoanDays));
            document.Summary.Add(new ReportMetric("Different borrowers", uniqueBorrowers.ToString(Inv)));

            // By category
            var byCategory = new ReportSection
            {
                Heading = "Loans by category",
                Columns = new List<string> { "Category", "Loans", "Returned", "Still out", "Overdue now", "Share of loans" },
                EmptyMessage = "No loans were made in this period."
            };
            foreach (var group in loans
                         .GroupBy(l => string.IsNullOrWhiteSpace(l.Item?.ItemType) ? "Unknown" : l.Item!.ItemType)
                         .OrderByDescending(g => g.Count())
                         .ThenBy(g => g.Key))
            {
                byCategory.Rows.Add(new List<string>
                {
                    group.Key,
                    group.Count().ToString(Inv),
                    group.Count(l => l.Status == LoanStatus.Returned).ToString(Inv),
                    group.Count(l => l.Status != LoanStatus.Returned).ToString(Inv),
                    group.Count(IsOverdue).ToString(Inv),
                    Percent(group.Count(), loans.Count)
                });
            }
            document.Sections.Add(byCategory);

            // Most borrowed
            var top = new ReportSection
            {
                Heading = "Most borrowed items (top 10)",
                Columns = new List<string> { "Rank", "Library code", "Title", "Category", "Times borrowed" },
                EmptyMessage = "No loans were made in this period."
            };
            var rank = 1;
            foreach (var entry in loans
                         .Where(l => l.Item != null)
                         .GroupBy(l => l.ItemId)
                         .Select(g => new { Item = g.First().Item!, Count = g.Count() })
                         .OrderByDescending(x => x.Count)
                         .ThenBy(x => x.Item.Name)
                         .Take(10))
            {
                top.Rows.Add(new List<string>
                {
                    rank.ToString(Inv),
                    entry.Item.LibraryCode,
                    entry.Item.Name,
                    entry.Item.ItemType,
                    entry.Count.ToString(Inv)
                });
                rank++;
            }
            document.Sections.Add(top);

            // Loan detail
            var detail = new ReportSection
            {
                Heading = "Loan detail",
                Columns = new List<string> { "Loan ID", "Borrowed", "Due", "Returned", "Library code", "Title", "Borrower", "Status" },
                EmptyMessage = "No loans were made in this period."
            };
            foreach (var loan in loans.OrderByDescending(l => l.BorrowDate).ThenByDescending(l => l.Id))
            {
                detail.Rows.Add(new List<string>
                {
                    loan.Id.ToString(Inv),
                    DateText(loan.BorrowDate),
                    DateText(loan.DueDate),
                    loan.ReturnDate.HasValue ? DateText(loan.ReturnDate.Value) : "-",
                    loan.Item?.LibraryCode ?? "-",
                    loan.Item?.Name ?? "(item removed)",
                    loan.Borrower?.FullName ?? "(borrower removed)",
                    loan.Status == LoanStatus.Returned ? "Returned" : IsOverdue(loan) ? "Overdue" : "Active"
                });
            }
            document.Sections.Add(detail);

            return document;
        }

        // =====================================================================
        // 2. Fine revenue audit
        // =====================================================================
        public async Task<ReportDocument> BuildFinesAsync(DateTime? from, DateTime? to, int? branchId = null)
        {
            var (start, endExclusive) = NormaliseRange(from, to);
            var branchName = await BranchNameAsync(branchId);

            var allFines = await _context.Fines
                .AsNoTracking()
                .Include(f => f.Borrower)
                .Include(f => f.Loan)
                    .ThenInclude(l => l!.Item)
                .ToListAsync();

            var fines = allFines
                .Where(f => (!start.HasValue || f.CreatedDate >= start.Value) &&
                            (!endExclusive.HasValue || f.CreatedDate < endExclusive.Value) &&
                            (!branchId.HasValue || f.Loan?.Item?.BranchId == branchId.Value))
                .ToList();

            var totalIssued = fines.Sum(f => f.Amount);
            var collected = fines.Where(f => f.IsPaid).Sum(f => f.Amount);
            var outstanding = fines.Where(f => !f.IsPaid).Sum(f => f.Amount);
            var totalDaysLate = fines.Sum(f => f.DaysLate);

            var document = new ReportDocument
            {
                Title = "Fine Revenue Audit",
                Subtitle = "Fines incurred in period: " + PeriodText(from, to) + BranchText(branchName),
                GeneratedUtc = DateTime.UtcNow
            };

            document.Summary.Add(new ReportMetric("Fines issued", fines.Count.ToString(Inv)));
            document.Summary.Add(new ReportMetric("Total value issued", Money(totalIssued)));
            document.Summary.Add(new ReportMetric("Collected (paid)", Money(collected)));
            document.Summary.Add(new ReportMetric("Outstanding (unpaid)", Money(outstanding)));
            document.Summary.Add(new ReportMetric("Collection rate", totalIssued == 0 ? "0.0%" : (100.0 * (double)(collected / totalIssued)).ToString("0.0", Inv) + "%"));
            document.Summary.Add(new ReportMetric("Average fine", fines.Count == 0 ? Money(0m) : Money(totalIssued / fines.Count)));
            document.Summary.Add(new ReportMetric("Total days overdue", totalDaysLate.ToString(Inv)));

            // Ledger
            var ledger = new ReportSection
            {
                Heading = "Fine ledger",
                Columns = new List<string> { "Fine ID", "Date incurred", "Borrower", "Card no.", "Item", "Days late", "Amount", "Status", "Date paid" },
                EmptyMessage = "No fines were incurred in this period."
            };
            foreach (var fine in fines.OrderByDescending(f => f.CreatedDate).ThenByDescending(f => f.Id))
            {
                var item = fine.Loan?.Item;
                ledger.Rows.Add(new List<string>
                {
                    fine.Id.ToString(Inv),
                    DateText(fine.CreatedDate),
                    fine.Borrower?.FullName ?? "(borrower removed)",
                    fine.Borrower?.CardNumber ?? "-",
                    item != null ? item.LibraryCode + " - " + item.Name : "(item removed)",
                    fine.DaysLate.ToString(Inv),
                    Money(fine.Amount),
                    fine.IsPaid ? "Paid" : "Unpaid",
                    fine.PaidDate.HasValue ? DateText(fine.PaidDate.Value) : "-"
                });
            }
            if (fines.Count > 0)
            {
                ledger.Rows.Add(new List<string> { "TOTAL", "", "", "", "", totalDaysLate.ToString(Inv), Money(totalIssued), "", "" });
            }
            document.Sections.Add(ledger);

            // Outstanding by borrower
            var debtors = new ReportSection
            {
                Heading = "Outstanding fines by borrower (top 10)",
                Columns = new List<string> { "Borrower", "Card no.", "Unpaid fines", "Amount owed" },
                EmptyMessage = "There are no unpaid fines in this period."
            };
            foreach (var group in fines
                         .Where(f => !f.IsPaid)
                         .GroupBy(f => f.BorrowerId)
                         .Select(g => new
                         {
                             Borrower = g.First().Borrower,
                             Count = g.Count(),
                             Amount = g.Sum(f => f.Amount)
                         })
                         .OrderByDescending(x => x.Amount)
                         .Take(10))
            {
                debtors.Rows.Add(new List<string>
                {
                    group.Borrower?.FullName ?? "(borrower removed)",
                    group.Borrower?.CardNumber ?? "-",
                    group.Count.ToString(Inv),
                    Money(group.Amount)
                });
            }
            document.Sections.Add(debtors);

            return document;
        }

        // =====================================================================
        // 3. Inventory health
        // =====================================================================
        public async Task<ReportDocument> BuildInventoryAsync(int? branchId = null)
        {
            var branchName = await BranchNameAsync(branchId);
            var allItems = await _context.Items.AsNoTracking().Include(i => i.Branch).ToListAsync();
            var items = allItems.Where(i => !branchId.HasValue || i.BranchId == branchId.Value).ToList();

            var openLoans = (await _context.Loans
                    .AsNoTracking()
                    .Include(l => l.Item)
                    .Include(l => l.Borrower)
                    .Where(l => l.Status != LoanStatus.Returned)
                    .ToListAsync())
                .Where(l => !branchId.HasValue || l.Item?.BranchId == branchId.Value)
                .ToList();

            var now = DateTime.UtcNow;
            var total = items.Count;
            var available = items.Count(i => i.Status == ItemStatus.Available);
            var borrowed = items.Count(i => i.Status == ItemStatus.Borrowed);
            var damaged = items.Count(i => i.Status == ItemStatus.Damaged);
            var destroyed = items.Count(i => i.Status == ItemStatus.Destroyed);
            var needAttention = items.Where(i => i.Status == ItemStatus.Damaged || i.Status == ItemStatus.Destroyed).ToList();
            var liability = needAttention.Sum(i => i.ReplacementCost);
            var totalValue = items.Sum(i => i.ReplacementCost);

            var document = new ReportDocument
            {
                Title = "Inventory Health Report",
                Subtitle = "Snapshot of the catalogue right now" + BranchText(branchName),
                GeneratedUtc = now
            };

            document.Summary.Add(new ReportMetric("Total catalogue items", total.ToString(Inv)));
            document.Summary.Add(new ReportMetric("Available", $"{available} ({Percent(available, total)})"));
            document.Summary.Add(new ReportMetric("Borrowed", $"{borrowed} ({Percent(borrowed, total)})"));
            document.Summary.Add(new ReportMetric("Damaged", $"{damaged} ({Percent(damaged, total)})"));
            document.Summary.Add(new ReportMetric("Destroyed", $"{destroyed} ({Percent(destroyed, total)})"));
            document.Summary.Add(new ReportMetric("Healthy stock (not damaged or destroyed)", Percent(total - needAttention.Count, total)));
            document.Summary.Add(new ReportMetric("Replacement liability (damaged + destroyed)", Money(liability)));
            document.Summary.Add(new ReportMetric("Total catalogue replacement value", Money(totalValue)));

            // Status by category
            var byCategory = new ReportSection
            {
                Heading = "Stock status by category",
                Columns = new List<string> { "Category", "Total", "Available", "Borrowed", "Damaged", "Destroyed", "Healthy stock" },
                EmptyMessage = "The catalogue has no items."
            };
            foreach (var group in items
                         .GroupBy(i => string.IsNullOrWhiteSpace(i.ItemType) ? "Unknown" : i.ItemType)
                         .OrderBy(g => g.Key))
            {
                var bad = group.Count(i => i.Status == ItemStatus.Damaged || i.Status == ItemStatus.Destroyed);
                byCategory.Rows.Add(new List<string>
                {
                    group.Key,
                    group.Count().ToString(Inv),
                    group.Count(i => i.Status == ItemStatus.Available).ToString(Inv),
                    group.Count(i => i.Status == ItemStatus.Borrowed).ToString(Inv),
                    group.Count(i => i.Status == ItemStatus.Damaged).ToString(Inv),
                    group.Count(i => i.Status == ItemStatus.Destroyed).ToString(Inv),
                    Percent(group.Count() - bad, group.Count())
                });
            }
            document.Sections.Add(byCategory);

            // Stock by branch (shows every branch, or only the selected one)
            var byBranch = new ReportSection
            {
                Heading = "Stock by branch",
                Columns = new List<string> { "Branch", "Total", "Available", "Borrowed", "Damaged", "Destroyed", "Healthy stock" },
                EmptyMessage = "The catalogue has no items."
            };
            foreach (var group in items
                         .GroupBy(i => i.Branch?.Name ?? "Not assigned")
                         .OrderBy(g => g.Key))
            {
                var bad = group.Count(i => i.Status == ItemStatus.Damaged || i.Status == ItemStatus.Destroyed);
                byBranch.Rows.Add(new List<string>
                {
                    group.Key,
                    group.Count().ToString(Inv),
                    group.Count(i => i.Status == ItemStatus.Available).ToString(Inv),
                    group.Count(i => i.Status == ItemStatus.Borrowed).ToString(Inv),
                    group.Count(i => i.Status == ItemStatus.Damaged).ToString(Inv),
                    group.Count(i => i.Status == ItemStatus.Destroyed).ToString(Inv),
                    Percent(group.Count() - bad, group.Count())
                });
            }
            document.Sections.Add(byBranch);

            // Items needing attention
            var attention = new ReportSection
            {
                Heading = "Items needing attention (damaged or destroyed)",
                Columns = new List<string> { "Library code", "Title", "Category", "Branch", "Status", "Replacement cost" },
                EmptyMessage = "No items are damaged or destroyed."
            };
            foreach (var item in needAttention.OrderByDescending(i => i.ReplacementCost).ThenBy(i => i.Name))
            {
                attention.Rows.Add(new List<string>
                {
                    item.LibraryCode,
                    item.Name,
                    item.ItemType,
                    item.Branch?.Name ?? "-",
                    item.Status.ToString(),
                    Money(item.ReplacementCost)
                });
            }
            if (needAttention.Count > 0)
            {
                attention.Rows.Add(new List<string> { "TOTAL", "", "", "", "", Money(liability) });
            }
            document.Sections.Add(attention);

            // Overdue items
            var overdue = new ReportSection
            {
                Heading = "Overdue items",
                Columns = new List<string> { "Library code", "Title", "Borrower", "Due date", "Days overdue" },
                EmptyMessage = "No items are overdue."
            };
            var overdueLoans = openLoans
                .Where(l => l.Status == LoanStatus.Overdue || l.DueDate < now)
                .OrderBy(l => l.DueDate)
                .ToList();
            foreach (var loan in overdueLoans)
            {
                var daysOverdue = Math.Max(0, (int)Math.Floor((now - loan.DueDate).TotalDays));
                overdue.Rows.Add(new List<string>
                {
                    loan.Item?.LibraryCode ?? "-",
                    loan.Item?.Name ?? "(item removed)",
                    loan.Borrower?.FullName ?? "(borrower removed)",
                    DateText(loan.DueDate),
                    daysOverdue.ToString(Inv)
                });
            }
            document.Sections.Add(overdue);

            return document;
        }

        // =====================================================================
        // helpers
        // =====================================================================

        /// <summary>Turns the optional From / To dates (inclusive) into a start and an exclusive end.</summary>
        private static (DateTime? Start, DateTime? EndExclusive) NormaliseRange(DateTime? from, DateTime? to)
        {
            if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
            {
                (from, to) = (to, from);
            }

            return (from?.Date, to?.Date.AddDays(1));
        }

        private static string PeriodText(DateTime? from, DateTime? to)
        {
            if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
            {
                (from, to) = (to, from);
            }

            if (from.HasValue && to.HasValue) return DateText(from.Value) + " to " + DateText(to.Value);
            if (from.HasValue) return "from " + DateText(from.Value);
            if (to.HasValue) return "up to " + DateText(to.Value);
            return "all dates";
        }

        private async Task<string?> BranchNameAsync(int? branchId)
        {
            if (!branchId.HasValue) return null;
            return await _context.Branches.Where(b => b.Id == branchId.Value).Select(b => b.Name).FirstOrDefaultAsync();
        }

        private static string BranchText(string? branchName) =>
            string.IsNullOrEmpty(branchName) ? string.Empty : " | Branch: " + branchName;

        private static string DateText(DateTime value) => value.ToString("yyyy-MM-dd", Inv);

        private static string Money(decimal value) =>
            value < 0 ? "-$" + Math.Abs(value).ToString("N2", Inv) : "$" + value.ToString("N2", Inv);

        private static string Percent(int part, int whole) =>
            whole == 0 ? "0.0%" : (100.0 * part / whole).ToString("0.0", Inv) + "%";
    }
}

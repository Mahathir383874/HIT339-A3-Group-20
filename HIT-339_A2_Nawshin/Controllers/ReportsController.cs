using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bibliotheca.Data;
using Bibliotheca.Models.ViewModels;
using Bibliotheca.Services;

namespace Bibliotheca.Controllers
{
    /// <summary>
    /// Manager reports (borrowing, fines, inventory health) with CSV and PDF export.
    /// </summary>
    [Authorize(Roles = "Manager,Admin")]
    public class ReportsController : Controller
    {
        private readonly ReportService _reports;
        private readonly ApplicationDbContext _context;

        public ReportsController(ReportService reports, ApplicationDbContext context)
        {
            _reports = reports;
            _context = context;
        }

        // GET: /Reports?from=2026-01-01&to=2026-12-31&branchId=1
        public async Task<IActionResult> Index(DateTime? from, DateTime? to, int? branchId = null)
        {
            if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
            {
                (from, to) = (to, from);
                TempData["InfoMessage"] = "The start date was after the end date, so the two dates were swapped.";
            }

            var model = new ReportsPageViewModel
            {
                From = from,
                To = to,
                BranchId = branchId,
                Branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync(),
                Borrowing = await _reports.BuildBorrowingAsync(from, to, branchId),
                Fines = await _reports.BuildFinesAsync(from, to, branchId),
                Inventory = await _reports.BuildInventoryAsync(branchId)
            };

            return View(model);
        }

        // GET: /Reports/Export?report=Borrowing&format=csv&from=2026-01-01&to=2026-12-31&branchId=1
        public async Task<IActionResult> Export(string? report, string? format, DateTime? from, DateTime? to, int? branchId = null)
        {
            if (!Enum.TryParse<ReportKind>(report, true, out var kind) || !Enum.IsDefined(kind))
            {
                return BadRequest("Unknown report. Use Borrowing, Fines or Inventory.");
            }

            var chosenFormat = (format ?? string.Empty).Trim().ToLowerInvariant();
            if (chosenFormat != "csv" && chosenFormat != "pdf")
            {
                return BadRequest("Unknown format. Use csv or pdf.");
            }

            var document = await _reports.BuildAsync(kind, from, to, branchId);
            var baseName = "bibliotheca-" + kind.ToString().ToLowerInvariant() + "-report-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmm");

            if (chosenFormat == "csv")
            {
                return File(ReportExporter.ToCsv(document), "text/csv", baseName + ".csv");
            }

            return File(ReportExporter.ToPdf(document), "application/pdf", baseName + ".pdf");
        }
    }
}

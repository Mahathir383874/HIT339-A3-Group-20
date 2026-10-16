using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bibliotheca.Models.ViewModels;
using Bibliotheca.Services;

namespace Bibliotheca.Controllers
{
    /// <summary>
    /// Admin utility for bulk-importing new catalogue items from a CSV file.
    /// Flow: upload + preview (nothing saved) -> confirm -> valid rows saved, invalid/duplicate rows reported.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class ImportController : Controller
    {
        private readonly CsvImportService _importService;

        public ImportController(CsvImportService importService)
        {
            _importService = importService;
        }

        // GET: /Import
        [HttpGet]
        public IActionResult Index()
        {
            return View(new ImportPageViewModel());
        }

        // GET: /Import/Template
        [HttpGet]
        public IActionResult Template()
        {
            return File(CsvImportService.BuildTemplate(), "text/csv", "bibliotheca_import_template.csv");
        }

        // POST: /Import/Preview  (validates only, saves nothing)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Preview(IFormFile? csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Please choose a CSV file to upload.";
                return RedirectToAction(nameof(Index));
            }

            var fileName = Path.GetFileName(csvFile.FileName);

            if (!string.Equals(Path.GetExtension(fileName), ".csv", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Only .csv files can be imported. Export your spreadsheet as CSV and try again.";
                return RedirectToAction(nameof(Index));
            }

            if (csvFile.Length > CsvImportService.MaxFileBytes)
            {
                TempData["ErrorMessage"] = $"The file is too large. The maximum size is {CsvImportService.MaxFileBytes / 1024} KB.";
                return RedirectToAction(nameof(Index));
            }

            string text;
            using (var reader = new StreamReader(csvFile.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                text = await reader.ReadToEndAsync();
            }

            var report = await _importService.ProcessAsync(text, fileName, commit: false);
            return View("Index", new ImportPageViewModel { Report = report });
        }

        // POST: /Import/Confirm  (re-validates on the server, then saves the valid rows)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(string? csvContent, string? fileName)
        {
            if (string.IsNullOrWhiteSpace(csvContent))
            {
                TempData["ErrorMessage"] = "There is no file to import. Please upload the file again.";
                return RedirectToAction(nameof(Index));
            }

            if (Encoding.UTF8.GetByteCount(csvContent) > CsvImportService.MaxFileBytes)
            {
                TempData["ErrorMessage"] = "The file is too large to import.";
                return RedirectToAction(nameof(Index));
            }

            var safeName = string.IsNullOrWhiteSpace(fileName) ? "uploaded.csv" : Path.GetFileName(fileName);

            var report = await _importService.ProcessAsync(csvContent, safeName, commit: true);

            if (report.Committed && report.ImportedCount > 0)
            {
                TempData["SuccessMessage"] = $"Imported {report.ImportedCount} item(s) from {safeName}.";
            }

            return View("Index", new ImportPageViewModel { Report = report });
        }
    }
}

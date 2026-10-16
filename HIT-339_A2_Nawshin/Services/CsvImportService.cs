using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Bibliotheca.Data;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.Enums;
using Bibliotheca.Models.ViewModels;

namespace Bibliotheca.Services
{
    /// <summary>
    /// Validates a CSV file of new catalogue items and (optionally) saves the valid rows.
    /// Invalid and duplicate rows are reported with the row number and the reasons; they never
    /// stop the rest of the file from being checked and never crash the application.
    /// Each row is validated with the same data-annotation rules the admin forms use.
    /// </summary>
    public class CsvImportService
    {
        public const int MaxFileBytes = 512 * 1024;
        public const int MaxDataRows = 1000;

        public static readonly string[] RequiredColumns = { "ItemType", "Name", "LibraryCode" };

        public static readonly string[] AllColumns =
        {
            "ItemType", "Name", "LibraryCode", "Description", "ReplacementCost", "ImageUrl",
            "Author", "Genre", "ISBN", "PageCount",
            "Artist", "ReleaseYear", "RecordLabel", "DurationMinutes",
            "ToyType", "RecommendedAge", "Manufacturer", "SafetyWarning"
        };

        private static readonly Regex LibraryCodePattern = new Regex(@"^[A-Z]{3}-\d{4}$", RegexOptions.Compiled);

        private readonly ApplicationDbContext _context;
        private readonly ILogger<CsvImportService> _logger;

        public CsvImportService(ApplicationDbContext context, ILogger<CsvImportService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>A small example file staff can download, fill in and upload.</summary>
        public static byte[] BuildTemplate()
        {
            var rows = new List<List<string>>
            {
                AllColumns.ToList(),
                new List<string> { "Book", "Example Book Title", "BOK-9901", "A short description", "30.00", "", "Jane Author", "Fiction", "978-0000000001", "320", "", "", "", "", "", "", "", "" },
                new List<string> { "Music", "Example Album", "MUS-9901", "", "20.00", "", "", "", "", "", "Example Band", "2021", "Example Records", "45", "", "", "", "" },
                new List<string> { "Toy", "Example Puzzle", "TOY-9901", "", "15.00", "", "", "", "", "", "", "", "", "", "Puzzle", "6+", "Example Toys", "Small parts" }
            };

            return CsvWriter.ToBytes(rows);
        }

        public async Task<ImportReport> ProcessAsync(string csvText, string fileName, bool commit)
        {
            var report = new ImportReport { FileName = fileName };

            // ---- 1. File level checks ----
            var text = (csvText ?? string.Empty).TrimStart('﻿');

            if (string.IsNullOrWhiteSpace(text))
            {
                report.FileErrors.Add("The file is empty.");
                return report;
            }

            if (text.Contains('\0'))
            {
                report.FileErrors.Add("This does not look like a plain-text CSV file (it contains binary data). Export your spreadsheet as CSV and try again.");
                return report;
            }

            List<List<string>> records;
            try
            {
                records = CsvParser.Parse(text);
            }
            catch (FormatException ex)
            {
                report.FileErrors.Add("The file could not be read as CSV: " + ex.Message);
                return report;
            }

            if (records.Count == 0)
            {
                report.FileErrors.Add("The file is empty.");
                return report;
            }

            var header = records[0].Select(h => h.Trim()).ToList();
            var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < header.Count; i++)
            {
                if (header[i].Length > 0 && !columnIndex.ContainsKey(header[i]))
                {
                    columnIndex[header[i]] = i;
                }
            }

            var missing = RequiredColumns.Where(c => !columnIndex.ContainsKey(c)).ToList();
            if (missing.Count > 0)
            {
                report.FileErrors.Add("The header row is missing required column(s): " + string.Join(", ", missing) +
                                      ". Download the template to see the expected columns.");
                return report;
            }

            var unknown = header.Where(h => h.Length > 0 && !AllColumns.Contains(h, StringComparer.OrdinalIgnoreCase)).ToList();
            if (unknown.Count > 0)
            {
                report.FileWarnings.Add("These columns are not recognised and were ignored: " + string.Join(", ", unknown) + ".");
            }

            var dataRecordCount = records.Count - 1;
            if (dataRecordCount == 0)
            {
                report.FileErrors.Add("The file has a header row but no data rows.");
                return report;
            }

            if (dataRecordCount > MaxDataRows)
            {
                report.FileErrors.Add($"The file has {dataRecordCount} data rows. The maximum per import is {MaxDataRows}.");
                return report;
            }

            report.CsvContent = text;

            // ---- 2. Row level checks ----
            var existingCodes = new HashSet<string>(
                await _context.Items.AsNoTracking().Select(i => i.LibraryCode).ToListAsync(),
                StringComparer.OrdinalIgnoreCase);

            var firstRowForCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var r = 1; r < records.Count; r++)
            {
                var cells = records[r];
                if (cells.All(c => string.IsNullOrWhiteSpace(c)))
                {
                    continue; // blank line
                }

                var rowNumber = r + 1; // header is row 1
                var result = EvaluateRow(rowNumber, cells, columnIndex, header.Count);

                if (result.LibraryCode.Length > 0)
                {
                    if (existingCodes.Contains(result.LibraryCode))
                    {
                        result.Messages.Add($"Library code {result.LibraryCode} already exists in the catalogue.");
                        result.Outcome = ImportRowOutcome.Duplicate;
                    }
                    else if (firstRowForCode.TryGetValue(result.LibraryCode, out var earlierRow))
                    {
                        result.Messages.Add($"Library code {result.LibraryCode} is repeated: it already appears in row {earlierRow} of this file.");
                        result.Outcome = ImportRowOutcome.Duplicate;
                    }
                }

                if (result.Outcome != ImportRowOutcome.Duplicate && result.Messages.Count > 0)
                {
                    result.Outcome = ImportRowOutcome.Invalid;
                }

                if (result.Outcome == ImportRowOutcome.Valid && result.LibraryCode.Length > 0)
                {
                    firstRowForCode[result.LibraryCode] = rowNumber;
                }

                if (result.Outcome != ImportRowOutcome.Valid)
                {
                    result.Item = null;
                }

                report.Rows.Add(result);
            }

            if (report.Rows.Count == 0)
            {
                report.FileErrors.Add("The file has no data rows (only blank lines after the header).");
                return report;
            }

            // ---- 3. Save valid rows (only when confirmed) ----
            if (commit)
            {
                var toImport = report.Rows
                    .Where(row => row.Outcome == ImportRowOutcome.Valid && row.Item != null)
                    .Select(row => row.Item!)
                    .ToList();

                if (toImport.Count > 0)
                {
                    try
                    {
                        await using var transaction = await _context.Database.BeginTransactionAsync();
                        _context.Items.AddRange(toImport);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        report.Committed = true;
                        report.ImportedCount = toImport.Count;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "CSV import failed while saving {Count} items.", toImport.Count);
                        report.FileErrors.Add("The items could not be saved, so nothing was imported. Please check the file and try again.");
                        _context.ChangeTracker.Clear();
                    }
                }
                else
                {
                    report.FileWarnings.Add("There were no valid rows to import.");
                }

                report.CsvContent = null;
            }

            return report;
        }

        private static ImportRowResult EvaluateRow(
            int rowNumber,
            IReadOnlyList<string> cells,
            Dictionary<string, int> columnIndex,
            int headerCount)
        {
            var result = new ImportRowResult { RowNumber = rowNumber };
            var messages = result.Messages;

            string Get(string column)
            {
                if (!columnIndex.TryGetValue(column, out var index) || index >= cells.Count)
                {
                    return string.Empty;
                }

                return cells[index].Trim();
            }

            string? GetOrNull(string column)
            {
                var value = Get(column);
                return value.Length == 0 ? null : value;
            }

            int? GetOptionalInt(string column)
            {
                var value = Get(column);
                if (value.Length == 0)
                {
                    return null;
                }

                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                {
                    return number;
                }

                messages.Add($"{column} '{value}' is not a whole number.");
                return null;
            }

            var typeText = Get("ItemType");
            var code = Get("LibraryCode").ToUpperInvariant();
            var name = Get("Name");

            result.ItemType = typeText;
            result.LibraryCode = code;
            result.Name = name;

            if (cells.Count > headerCount)
            {
                messages.Add($"The row has {cells.Count} values but the header has {headerCount} columns.");
            }

            Item? item = null;
            switch (typeText.ToLowerInvariant())
            {
                case "book":
                    item = new Book
                    {
                        Author = Get("Author"),
                        Genre = Get("Genre"),
                        ISBN = GetOrNull("ISBN"),
                        PageCount = GetOptionalInt("PageCount")
                    };
                    break;
                case "music":
                    item = new Music
                    {
                        Artist = Get("Artist"),
                        ReleaseYear = GetOptionalInt("ReleaseYear"),
                        RecordLabel = GetOrNull("RecordLabel"),
                        DurationMinutes = GetOptionalInt("DurationMinutes")
                    };
                    break;
                case "toy":
                    item = new Toy
                    {
                        ToyType = Get("ToyType"),
                        RecommendedAge = Get("RecommendedAge"),
                        Manufacturer = GetOrNull("Manufacturer"),
                        SafetyWarning = GetOrNull("SafetyWarning")
                    };
                    break;
                default:
                    messages.Add(typeText.Length == 0
                        ? "ItemType is required (Book, Music or Toy)."
                        : $"ItemType '{typeText}' is not valid. Use Book, Music or Toy.");
                    break;
            }

            // Optional shared fields
            var costText = Get("ReplacementCost");
            decimal? cost = null;
            if (costText.Length > 0)
            {
                if (decimal.TryParse(costText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedCost))
                {
                    cost = parsedCost;
                }
                else
                {
                    messages.Add($"ReplacementCost '{costText}' is not a valid number (use digits and a decimal point, for example 25.00).");
                }
            }

            var imageUrl = GetOrNull("ImageUrl");
            if (imageUrl != null)
            {
                var validUrl = Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) &&
                               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
                if (!validUrl)
                {
                    messages.Add("ImageUrl must be a full http:// or https:// address.");
                    imageUrl = null;
                }
            }

            if (item == null)
            {
                // Unknown type: still report the other common problems so the user can fix everything at once.
                if (name.Length == 0)
                {
                    messages.Add("Item title/name is required.");
                }

                if (!LibraryCodePattern.IsMatch(code))
                {
                    messages.Add("Library code must be in the standard format (for example BOK-1001, MUS-2001, TOY-3001).");
                }

                return result;
            }

            item.Name = name;
            item.LibraryCode = code;
            item.Description = GetOrNull("Description");
            item.ImageUrl = imageUrl;
            item.Status = ItemStatus.Available;
            if (cost.HasValue)
            {
                item.ReplacementCost = cost.Value;
            }

            // Same rules as the admin create forms (required fields, ranges, library code format, lengths)
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(item, new ValidationContext(item), validationResults, validateAllProperties: true);
            foreach (var validation in validationResults)
            {
                messages.Add(validation.ErrorMessage ?? "A value is not valid.");
            }

            result.Item = item;
            return result;
        }
    }
}

using Bibliotheca.Models.Entities;

namespace Bibliotheca.Models.ViewModels
{
    public enum ImportRowOutcome
    {
        Valid,
        Invalid,
        Duplicate
    }

    public class ImportRowResult
    {
        /// <summary>Record number in the file. The header is row 1, so the first data row is row 2.</summary>
        public int RowNumber { get; set; }
        public string ItemType { get; set; } = string.Empty;
        public string LibraryCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ImportRowOutcome Outcome { get; set; } = ImportRowOutcome.Valid;
        public List<string> Messages { get; set; } = new List<string>();

        /// <summary>The item that will be created if this row is valid (not shown in the UI).</summary>
        public Item? Item { get; set; }
    }

    public class ImportReport
    {
        public string FileName { get; set; } = string.Empty;

        /// <summary>True when the valid rows were saved to the database in this request.</summary>
        public bool Committed { get; set; }

        /// <summary>Problems with the file as a whole (empty, missing columns, too large ...).</summary>
        public List<string> FileErrors { get; set; } = new List<string>();

        public List<string> FileWarnings { get; set; } = new List<string>();

        public List<ImportRowResult> Rows { get; set; } = new List<ImportRowResult>();

        public int ImportedCount { get; set; }

        /// <summary>The validated CSV text, echoed back so the preview can be confirmed without re-uploading.</summary>
        public string? CsvContent { get; set; }

        public bool HasFileErrors => FileErrors.Count > 0;
        public int TotalRows => Rows.Count;
        public int ValidCount => Rows.Count(r => r.Outcome == ImportRowOutcome.Valid);
        public int InvalidCount => Rows.Count(r => r.Outcome == ImportRowOutcome.Invalid);
        public int DuplicateCount => Rows.Count(r => r.Outcome == ImportRowOutcome.Duplicate);
    }

    public class ImportPageViewModel
    {
        public ImportReport? Report { get; set; }
    }
}

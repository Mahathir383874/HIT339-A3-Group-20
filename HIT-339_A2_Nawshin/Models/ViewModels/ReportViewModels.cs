namespace Bibliotheca.Models.ViewModels
{
    /// <summary>One headline figure (label + formatted value) shown at the top of a report.</summary>
    public class ReportMetric
    {
        public ReportMetric(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; set; }
        public string Value { get; set; }
    }

    /// <summary>A titled table inside a report. All cells are pre-formatted text so the same data
    /// can be shown on screen, written to CSV and drawn into a PDF.</summary>
    public class ReportSection
    {
        public string Heading { get; set; } = string.Empty;
        public List<string> Columns { get; set; } = new List<string>();
        public List<List<string>> Rows { get; set; } = new List<List<string>>();
        public string EmptyMessage { get; set; } = "No data for this report.";
    }

    public class ReportDocument
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;
        public List<ReportMetric> Summary { get; set; } = new List<ReportMetric>();
        public List<ReportSection> Sections { get; set; } = new List<ReportSection>();
    }

    public class ReportsPageViewModel
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int? BranchId { get; set; }
        public List<Bibliotheca.Models.Entities.Branch> Branches { get; set; } = new List<Bibliotheca.Models.Entities.Branch>();
        public ReportDocument Borrowing { get; set; } = new ReportDocument();
        public ReportDocument Fines { get; set; } = new ReportDocument();
        public ReportDocument Inventory { get; set; } = new ReportDocument();
    }

    public class ReportCardViewModel
    {
        /// <summary>Borrowing, Fines or Inventory (matches the Export action's "report" value).</summary>
        public string Kind { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-bar-chart";
        public bool UsesDateFilter { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public int? BranchId { get; set; }
        public ReportDocument Document { get; set; } = new ReportDocument();
    }
}

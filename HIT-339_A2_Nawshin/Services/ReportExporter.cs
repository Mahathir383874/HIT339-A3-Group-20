using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Bibliotheca.Models.ViewModels;

namespace Bibliotheca.Services
{
    /// <summary>
    /// Turns a ReportDocument into a CSV file or a PDF file.
    /// PDF generation uses QuestPDF (Community licence).
    /// </summary>
    public static class ReportExporter
    {
        static ReportExporter()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // -------------------------------------------------------------
        // CSV
        // -------------------------------------------------------------
        public static byte[] ToCsv(ReportDocument report)
        {
            var rows = new List<List<string>>
            {
                new List<string> { report.Title },
                new List<string> { report.Subtitle },
                new List<string> { "Generated (UTC)", report.GeneratedUtc.ToString("yyyy-MM-dd HH:mm") },
                new List<string>(),
                new List<string> { "Summary" },
                new List<string> { "Metric", "Value" }
            };

            foreach (var metric in report.Summary)
            {
                rows.Add(new List<string> { metric.Label, metric.Value });
            }

            foreach (var section in report.Sections)
            {
                rows.Add(new List<string>());
                rows.Add(new List<string> { section.Heading });
                rows.Add(section.Columns.ToList());

                if (section.Rows.Count == 0)
                {
                    rows.Add(new List<string> { section.EmptyMessage });
                    continue;
                }

                foreach (var row in section.Rows)
                {
                    rows.Add(row);
                }
            }

            return CsvWriter.ToBytes(rows);
        }

        // -------------------------------------------------------------
        // PDF
        // -------------------------------------------------------------
        public static byte[] ToPdf(ReportDocument report)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(28);
                    page.DefaultTextStyle(style => style.FontSize(9));

                    page.Header().Column(column =>
                    {
                        column.Item().Text("Bibliotheca Library Management System").FontSize(9).FontColor(Colors.Grey.Medium);
                        column.Item().Text(report.Title).FontSize(18).Bold().FontColor(Colors.Blue.Darken3);
                        column.Item().Text(report.Subtitle).FontColor(Colors.Grey.Darken1);
                        column.Item().PaddingBottom(8).Text("Generated " + report.GeneratedUtc.ToString("yyyy-MM-dd HH:mm") + " UTC")
                            .FontSize(8).FontColor(Colors.Grey.Medium);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(14);

                        // Summary table
                        column.Item().Column(summary =>
                        {
                            summary.Item().PaddingBottom(4).Text("Summary").FontSize(12).Bold();
                            summary.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                });

                                foreach (var metric in report.Summary)
                                {
                                    table.Cell().Element(BodyCell).Text(metric.Label);
                                    table.Cell().Element(BodyCell).Text(metric.Value).SemiBold();
                                }
                            });
                        });

                        // Data sections
                        foreach (var section in report.Sections)
                        {
                            column.Item().Column(block =>
                            {
                                block.Item().PaddingBottom(4).Text(section.Heading).FontSize(12).Bold();

                                if (section.Rows.Count == 0)
                                {
                                    block.Item().Text(section.EmptyMessage).Italic().FontColor(Colors.Grey.Darken1);
                                    return;
                                }

                                var columnCount = section.Columns.Count;

                                block.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        for (var i = 0; i < columnCount; i++)
                                        {
                                            columns.RelativeColumn();
                                        }
                                    });

                                    table.Header(header =>
                                    {
                                        foreach (var heading in section.Columns)
                                        {
                                            header.Cell().Element(HeaderCell).Text(heading);
                                        }
                                    });

                                    foreach (var row in section.Rows)
                                    {
                                        for (var i = 0; i < columnCount; i++)
                                        {
                                            var text = i < row.Count ? row[i] : string.Empty;
                                            table.Cell().Element(BodyCell).Text(text ?? string.Empty);
                                        }
                                    }
                                });
                            });
                        }
                    });

                    page.Footer().AlignCenter().DefaultTextStyle(style => style.FontSize(8)).Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static IContainer HeaderCell(IContainer container) =>
            container
                .DefaultTextStyle(style => style.SemiBold().FontColor(Colors.White))
                .Background(Colors.Blue.Darken3)
                .PaddingVertical(4)
                .PaddingHorizontal(5);

        private static IContainer BodyCell(IContainer container) =>
            container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten2)
                .PaddingVertical(3)
                .PaddingHorizontal(5);
    }
}

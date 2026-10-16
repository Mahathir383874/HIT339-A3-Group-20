# Moon – Importer and Reporting Notes

## Part 1: CSV importer

**Where:** sign in as **Admin** (`admin@bibliotheca.com`) → *Item Management* → *Bulk Import (CSV)*, or `/Import`.  
**Code:** `Controllers/ImportController.cs`, `Services/CsvImportService.cs`, `Services/CsvParser.cs`, `Views/Import/Index.cshtml`.

### How it works

1. **Upload and preview.** The admin chooses a `.csv` file and presses *Validate & preview*. Nothing is saved at this step.
2. **Validation.** Every data row is checked and labelled **Valid**, **Invalid** or **Duplicate**, with the row number (the header is row 1) and the reasons.
3. **Confirm.** *Import N valid row(s)* checks the file again on the server (the preview is never trusted) and saves only the valid rows in a single database transaction.
4. **Result.** The page shows how many items were imported and still lists every skipped row.

### File format

| Column | Required | Notes |
|---|---|---|
| `ItemType` | Yes | `Book`, `Music` or `Toy` (not case sensitive) |
| `Name` | Yes | Max 120 characters |
| `LibraryCode` | Yes | Format `AAA-0000`, e.g. `BOK-9001`. Lower case is converted to upper case. |
| `Description` | No | Max 500 characters |
| `ReplacementCost` | No | Number such as `25.00` (default 25.00), 0 to 10,000 |
| `ImageUrl` | No | Must be a full `http://` or `https://` address |
| `Author`, `Genre` | Book: yes | `ISBN` (max 20), `PageCount` (1 to 10,000) optional |
| `Artist` | Music: yes | `ReleaseYear` (1900 to 2099), `RecordLabel`, `DurationMinutes` (1 to 600) optional |
| `ToyType`, `RecommendedAge` | Toy: yes | `Manufacturer`, `SafetyWarning` optional |

Column order does not matter, columns are matched by header name. Unknown columns are ignored with a warning. New items always start as *Available*. A template can be downloaded from the page.

### Validation rules

- **Row rules** come from the data-annotation attributes already on `Item`, `Book`, `Music` and `Toy`, so the importer and the admin forms accept and reject exactly the same values.
- **Duplicates:** a library code that already exists in the catalogue, or one repeated earlier in the same file (the first valid row wins).
- **File rules:** not empty, not binary, valid CSV (quotes closed), required columns present, at least one data row, at most 1,000 data rows, at most 512 KB, `.csv` extension.

### Expected results for the sample files (`docs/moon/sample-data/`)

These expected results assume the original seeded catalogue (codes `BOK-1001` to `TOY-3004`).

| File | Expected outcome |
|---|---|
| `valid_import.csv` | 5 rows, all valid. Import adds 2 books, 1 music album, 2 toys. Uploading it again shows 5 duplicates. |
| `mixed_import.csv` | 10 rows: **3 valid** (rows 2, 3, 9), **5 invalid** (row 4 missing author and bad code, row 7 unknown type, row 8 text page count, row 10 release year 1800, row 11 bad cost and image URL), **2 duplicate** (row 5 exists in catalogue, row 6 repeats row 2's code). Import adds only the 3 valid rows. |
| `missing_columns.csv` | File error: required columns `ItemType`, `Name`, `LibraryCode` missing. Nothing imported. |
| `header_only.csv` | File error: header row but no data rows. |
| `unterminated_quote.csv` | File error: a quoted value is never closed. |
| `empty.csv` | Message asking for a file (a 0-byte upload is refused before reading). |
| A renamed `.xlsx` as `.csv` | File error: file contains binary data. |
| A `.txt` file | Message: only `.csv` files can be imported. |

### Security and safety

- Admin only (`[Authorize(Roles = "Admin")]`), same role as manual item management. Change the role string in `ImportController` if the team decides otherwise.
- Anti-forgery tokens on every POST. File type, size and row limits stop oversized uploads.
- `ImageUrl` must be http/https, which stops `javascript:` style links being stored.
- The importer never crashes on bad data: every problem becomes a message on the page. If the database save itself fails, nothing is imported (transaction) and a general error is shown and logged.

### Not done (and why)

- The brief offers CSV upload **or** a mock external metadata provider. Only CSV was built, which satisfies the guide. A mock provider could be added later as another action on `ImportController`.

---

## Part 2: Manager reports and export

**Where:** sign in as **Manager** (`manager@bibliotheca.com`) or Admin → *Reports & Export*, or `/Reports`. The Manager dashboard also has a button linking to it.  
**Code:** `Controllers/ReportsController.cs`, `Services/ReportService.cs`, `Services/ReportExporter.cs`, `Services/CsvWriter.cs`, `Views/Reports/`.

### How it is built

`ReportService` calculates each report once into a `ReportDocument` (title, headline figures, and tables of text). The same object is shown on screen, written to CSV (`ReportExporter.ToCsv`) and drawn to PDF (`ReportExporter.ToPdf`, QuestPDF). Because there is only one calculation, the screen, the CSV and the PDF cannot disagree.

### Report definitions

| Report | Date filter | Headline figures | Tables |
|---|---|---|---|
| **Borrowing statistics** | Loan date | Loans, returned, still out, overdue now, return rate, average loan length, different borrowers | Loans by category; most borrowed items (top 10); loan detail |
| **Fine revenue audit** | Date the fine was incurred | Fines issued, total value, collected, outstanding, collection rate, average fine, total days overdue | Fine ledger with totals row; outstanding fines by borrower (top 10) |
| **Inventory health** | None (snapshot of today) | Total items, available, borrowed, damaged, destroyed, healthy stock %, replacement liability, total replacement value | Stock status by category; damaged/destroyed items with totals row; overdue items |

Definitions used:

- **Overdue** matches the Manager dashboard: a loan with status *Overdue*, or an *Active* loan whose due date has passed.
- **Healthy stock** = items that are not damaged or destroyed.
- **Replacement liability** = replacement cost of damaged and destroyed items.
- **Collection rate** = paid fine value ÷ total fine value in the period.
- Dates in the filter are whole days; the end date is inclusive. If From is after To, the two are swapped.

### Export

- `GET /Reports/Export?report=Borrowing|Fines|Inventory&format=csv|pdf&from=yyyy-MM-dd&to=yyyy-MM-dd`
- **CSV:** UTF-8 with a byte-order mark so Excel shows all characters correctly. The file holds the title, summary and each table one after another. Text that starts with `=`, `+`, `@` (or a minus before a non-number) gets a leading `'` so it can never run as a spreadsheet formula.
- **PDF:** A4 landscape, header with title and period, summary table, each table with a coloured header row, and page numbers in the footer.
- Empty reports still export, with "No ... in this period" messages.
- Only Manager and Admin can use the page and the export links.

### Adding branch filtering later (REQ-ANA-06)

After Taief's branch model is merged, add an optional branch id to `ReportsController.Index/Export` and to the `BuildXxxAsync` methods in `ReportService`, filter the loaded loans, items and fines by the item's branch before the figures are calculated, and add a drop-down to `Views/Reports/Index.cshtml`. The exporters do not need to change.

### Licence note

PDF export uses QuestPDF under its Community licence, which allows free use for education and small organisations. The licence is set in the static constructor of `ReportExporter`.

---

## Part 3: README text for the team README

Suggested lines for the "how to test the modules" section of the main `README.md` (Achintha owns the README setup):

```markdown
### API, importer and reports (Moon)
- API documentation page: `/api-docs.html` (OpenAPI document at `/openapi/v1.json`). Try `GET /api/v1/items`, `/api/v1/categories`, `/api/v1/status`.
- CSV import: sign in as Admin → Item Management → Bulk Import (CSV). Sample files are in `docs/moon/sample-data/`.
- Reports and export: sign in as Manager → Reports & Export. Use the Export CSV / Export PDF buttons on each report.
```

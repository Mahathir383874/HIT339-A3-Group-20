# Moon – Test Plan, Evidence Log and Task Hours

The **Result** and **Evidence** columns are left blank on purpose: fill them in when you run each test (pass/fail, plus the screenshot file name or commit link). Actual hours must be recorded as you work, not guessed later.

Test data: the seeded database created on first run (staff accounts and 13 items), plus the files in `docs/moon/sample-data/`.

## 1. API tests (`feature/moon-api`)

| Test | Steps | Expected | Result | Evidence |
|---|---|---|---|---|
| TC-API-01 | `GET /api/v1/items` | 200, JSON with paging fields and items | | |
| TC-API-02 | `GET /api/v1/items?category=Book&status=Available&pageSize=2` | 200, only available books, at most 2 per page, `totalPages` correct | | |
| TC-API-03 | `GET /api/v1/items?search=hobbit` and `?search=zzzz` | First returns The Hobbit; second returns 200 with an empty `items` list | | |
| TC-API-04 | `GET /api/v1/items/1` | 200, one item with type-specific fields | | |
| TC-API-05 | `GET /api/v1/items/99999` | 404 problem-details JSON | | |
| TC-API-06 | `GET /api/v1/items/0`, `/-5`, `/abc` | 400 each | | |
| TC-API-07 | `GET /api/v1/categories` | 200, Book, Music, Toy with counts | | |
| TC-API-08 | `GET /api/v1/status` | 200, status "Operational", counts for all four item statuses | | |
| TC-API-09 | `GET /api/v1/items?category=Magazine` and `?status=Lost` | 400, message lists allowed values | | |
| TC-API-10 | `GET /api/v1/items?page=0` and `?pageSize=500` | 400 each | | |
| TC-API-11 | `DELETE /api/v1/items/1` and `POST /api/v1/items` | 405, catalogue unchanged | | |
| TC-API-12 | Read any item response | No replacement cost, loans, borrowers or fines present | | |
| TC-API-13 | `GET /api/v1/items/by-code/BOK-1001`, `/by-code/xyz`, `/by-code/BOK-9999` | 200, 400, 404 | | |
| TC-API-14 | Open `/openapi/v1.json` and `/api-docs.html` | JSON document loads; page lists the endpoints (needs internet for the page's style/script files) | | |

## 2. Importer tests (`feature/moon-import`)

| Test | Steps | Expected | Result | Evidence |
|---|---|---|---|---|
| TC-IMP-01 | Open `/Import` as Admin, Reception, Manager and when signed out | Admin sees the page; others are refused or redirected to login | | |
| TC-IMP-02 | Upload `valid_import.csv`, check the preview, press Import | 5 valid, 5 items added and visible in the catalogue | | |
| TC-IMP-03 | Upload `mixed_import.csv`, check the preview, press Import | 3 valid / 5 invalid / 2 duplicate with reasons; only 3 items added | | |
| TC-IMP-04a | Upload `empty.csv` | Message asking for a file; nothing imported | | |
| TC-IMP-04b | Upload `header_only.csv` | File error: no data rows | | |
| TC-IMP-04c | Upload `missing_columns.csv` | File error: required columns missing | | |
| TC-IMP-04d | Upload `unterminated_quote.csv` | File error: quote never closed | | |
| TC-IMP-04e | Rename a `.xlsx` to `.csv` and upload | File error: binary data | | |
| TC-IMP-04f | Upload a `.txt` file | Message: only .csv files | | |
| TC-IMP-04g | Upload a CSV larger than 512 KB | Message: file too large | | |
| TC-IMP-05 | Upload `valid_import.csv` again after TC-IMP-02 | 5 duplicates, 0 valid, nothing added | | |
| TC-IMP-06 | Download the template, upload it unchanged | 3 valid rows (codes `BOK-9901`, `MUS-9901`, `TOY-9901`); do not confirm unless you want these sample items | | |

## 3. Analytics and export tests (`feature/moon-analytics`)

| Test | Steps | Expected | Result | Evidence |
|---|---|---|---|---|
| TC-ANA-01 | Sign in as Manager, open Reports & Export; compare with the Manager dashboard | Three reports shown; loan, overdue, fine and inventory totals agree with the dashboard | | |
| TC-ANA-02 | Set From/To to a narrow range, then Clear; then set From later than To | Figures change then restore; dates are swapped with a message | | |
| TC-ANA-03 | Open `/Reports` as Reception and signed out | Access denied / redirect to login | | |
| TC-EXP-01 | Export CSV for Borrowing, Fines, Inventory; open in Excel | Three files open with summary and tables, no garbled characters | | |
| TC-EXP-02 | Export PDF for Borrowing, Fines, Inventory; open in a viewer | Three files open with title, summary, tables and page numbers | | |
| TC-EXP-03 | Filter to a date range with no data (for example 2000-01-01 to 2000-01-02); export CSV and PDF of Borrowing and Fines | Files still open, with "No ... in this period" messages | | |

## 4. General

| Test | Steps | Expected | Result | Evidence |
|---|---|---|---|---|
| TC-GEN-01 | `dotnet build`; then use the public catalogue, login, issue/return a loan, Manager dashboard | Build succeeds; existing features unchanged | | |

## 5. Individual task evidence (guide section 9)

Planned hours are a **proposal** based on the earlier estimate; change them to match what the team agrees. Leave Actual hours blank until the work is done.

| Task ID | Task | Planned hours | Actual hours | Evidence |
|---|---|---|---|---|
| MOON-01 | Read-only API: items, item by id/code, categories, status, error handling | 8 | | Commits on `feature/moon-api`, TC-API-01 to 13 |
| MOON-02 | OpenAPI document and API documentation page | 2 | | TC-API-14 |
| MOON-03 | CSV importer: upload, validation, preview, confirm, reports | 10 | | Commits on `feature/moon-import`, TC-IMP-01 to 06 |
| MOON-04 | Analytics: borrowing, fines, inventory reports with date filter | 8 | | Commits on `feature/moon-analytics`, TC-ANA-01 to 03 |
| MOON-05 | CSV and PDF export | 6 | | TC-EXP-01 to 03 |
| MOON-06 | Requirements list, acceptance criteria, traceability table | 4 | | `01_Requirements_and_Traceability.md` |
| MOON-07 | API, importer and reporting notes | 3 | | `02_...` and `03_...` notes |
| MOON-08 | Testing, screenshots and evidence | 5 | | This log |
| MOON-09 | Project plan (with Taief) and integration diagrams (with Taief) | 8 | | Portfolio sections |
| MOON-10 | Meetings, merge fixes and final review | 6 | | Meeting minutes, merge commits |

## 6. Suggested Git commits (one branch per feature, as in the guide)

```powershell
git checkout main
git pull

# API
git checkout -b feature/moon-api
git add HIT-339_A2_Nawshin/Controllers/Api HIT-339_A2_Nawshin/Models/Api HIT-339_A2_Nawshin/wwwroot/api-docs.html HIT-339_A2_Nawshin/Program.cs HIT-339_A2_Nawshin/Bibliotheca.csproj docs/moon/02_API_Endpoint_Notes.md
git commit -m "Add public read-only REST API with OpenAPI docs"
```

Program.cs and the .csproj contain changes for all three features. If you want strictly separate branches, stage only the matching lines with `git add -p`. The three feature groups are:

- **API:** the files above plus the `AddOpenApi`, `AddCors`, `UseCors`, `MapOpenApi` lines in `Program.cs` and the `Microsoft.AspNetCore.OpenApi` package.
- **Import:** `Controllers/ImportController.cs`, `Services/CsvImportService.cs`, `Services/CsvParser.cs`, `Services/CsvWriter.cs`, `Models/ViewModels/ImportViewModels.cs`, `Views/Import/`, the *Bulk Import* link in `_Layout.cshtml`, `docs/moon/sample-data/`, and the `CsvImportService` line in `Program.cs`.
- **Analytics:** `Controllers/ReportsController.cs`, `Services/ReportService.cs`, `Services/ReportExporter.cs`, `Services/CsvWriter.cs`, `Models/ViewModels/ReportViewModels.cs`, `Views/Reports/`, the *Reports & Export* links in `_Layout.cshtml` and `Views/Manager/Index.cshtml`, the `ReportService` line in `Program.cs` and the `QuestPDF` package.

`CsvWriter.cs` is shared by import (template) and analytics (export): commit it with whichever branch you merge first.

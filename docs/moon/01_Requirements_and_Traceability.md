# Moon – Requirements, Acceptance Criteria and Traceability

**Member:** Mishkatuz Zannat Moon (student ID 385411)  
**Main responsibility:** Read-only REST API, External Data Importer, Manager Analytics and Export  
**Branches:** `feature/moon-api`, `feature/moon-import`, `feature/moon-analytics`  
**Source of requirements:** Group Project brief, Part B (*Public API & Developer Endpoint*, *External Book/Media Data Importer*, *Expanded Manager Analytics & Export*) and the Work Distribution Guide, section 3.

Priority: **M** = must have (stated in the brief or guide), **S** = should have (value-adding work listed in the guide).

---

## 1. Requirements

### 1.1 Public read-only API

| ID | Requirement | Priority | Source |
|---|---|---|---|
| REQ-API-01 | The system provides a GET endpoint that lists catalogue items, with paging and optional filters (category, status, search text). | M | Brief B1; Guide §3 |
| REQ-API-02 | The system provides a GET endpoint that returns one catalogue item by its id. | M | Brief B1 |
| REQ-API-03 | The system provides a GET endpoint that lists the catalogue categories with item counts. | M | Brief B1; Guide §3 |
| REQ-API-04 | The system provides a GET endpoint that reports the operating status of the service and a summary of stock by status. | M | Brief B1; Guide §3 |
| REQ-API-05 | All API responses are JSON and use suitable HTTP status codes (200, 400, 404, 503) for valid and invalid requests. | M | Guide §3 |
| REQ-API-06 | The API is read-only: it exposes no create, update or delete operations. Other HTTP verbs are refused. | M | Brief B1 ("read-only"); Guide §3 |
| REQ-API-07 | The API never exposes borrower, loan or fine data, or internal item data (replacement cost). | M | Brief B1 ("secure") |
| REQ-API-08 | An item can also be requested by its library code (for example `BOK-1001`). | S | Added by Moon |
| REQ-API-09 | The API is documented (OpenAPI document and a browsable documentation page). | S | Guide §3 (Swagger/OpenAPI) |
| REQ-API-10 | The item list supports pagination and basic filters. | S | Guide §3 |

### 1.2 External data importer (CSV)

| ID | Requirement | Priority | Source |
|---|---|---|---|
| REQ-IMP-01 | Admin staff can upload a CSV file to bulk-import new Book, Music and Toy items. Other roles and anonymous users cannot. | M | Brief B3; Guide §3 |
| REQ-IMP-02 | Each row is validated with the same rules as the manual item forms (required fields, library code format, numeric ranges, text lengths). | M | Guide §3 |
| REQ-IMP-03 | Invalid rows are reported with the row number and the reason(s); they do not crash the application or stop other rows being checked. | M | Guide §3 |
| REQ-IMP-04 | Duplicate rows are reported: a library code that already exists in the catalogue, or that is repeated inside the file. | M | Guide §3 |
| REQ-IMP-05 | Problem files (empty, wrong type, binary, missing required columns, header only, too large, too many rows, unclosed quote) are rejected with a clear message and nothing is imported. | M | Guide §3 |
| REQ-IMP-06 | Only valid rows are saved, in one database transaction (all valid rows or none). | M | Guide §3 |
| REQ-IMP-07 | The user sees a preview and validation summary before anything is saved, and confirms the import. | S | Guide §3 (CSV import preview) |
| REQ-IMP-08 | A CSV template can be downloaded. | S | Added by Moon |

### 1.3 Manager analytics and export

| ID | Requirement | Priority | Source |
|---|---|---|---|
| REQ-ANA-01 | A borrowing statistics report shows loans, returns, items still out, overdue items, return rate, average loan length, loans by category, most borrowed items and loan detail. | M | Brief B3; Guide §3 |
| REQ-ANA-02 | A fine revenue audit shows fines issued, collected and outstanding, collection rate, a fine ledger and the borrowers who owe the most. | M | Brief B3; Guide §3 |
| REQ-ANA-03 | An inventory health report shows stock by status and category, damaged/destroyed items with replacement liability, and overdue items. | M | Brief B3; Guide §3 |
| REQ-ANA-04 | The borrowing and fines reports can be filtered by date range. | S | Guide §3 |
| REQ-ANA-05 | Reports are available only to the Manager and Admin roles. | M | Existing role model (Assignment 2) |
| REQ-EXP-01 | Every report can be exported as a CSV file. | M | Brief B3; Guide §3 |
| REQ-EXP-02 | Every report can be exported as a PDF file. | M | Brief B3; Guide §3 |
| REQ-EXP-03 | Export files open correctly, including when the report has no data. CSV values that could run as spreadsheet formulas are neutralised. | M | Guide §3 ("generated and opened") |
| REQ-ANA-06 | Branch filtering can be added to the reports once the multi-branch model is complete. | S | Guide §3 ("later if needed") |

### 1.4 General

| ID | Requirement | Priority |
|---|---|---|
| REQ-GEN-01 | The solution builds without errors and the existing Assignment 2 features (catalogue, loans, fines, manager dashboard) still work. | M |
| REQ-GEN-02 | New modules reuse the existing database context, entities and role setup instead of duplicating them. | M |

---

## 2. Acceptance criteria

| ID | Acceptance criterion | Covers |
|---|---|---|
| AC-API-01 | `GET /api/v1/items` returns HTTP 200 and JSON with `page`, `pageSize`, `totalCount`, `totalPages` and `items`. | REQ-API-01, 05, 10 |
| AC-API-02 | `GET /api/v1/items?category=Book` returns only books; `?status=Available` returns only available items; `?search=hobbit` returns matching items. | REQ-API-01, 10 |
| AC-API-03 | `GET /api/v1/items/{id}` returns HTTP 200 with the item for an existing id, HTTP 404 for an id that does not exist, and HTTP 400 for `0`, a negative number or text. | REQ-API-02, 05 |
| AC-API-04 | `GET /api/v1/categories` returns Book, Music and Toy with total and available counts. | REQ-API-03 |
| AC-API-05 | `GET /api/v1/status` returns HTTP 200 with the service status, server time, total items, active loans and counts for every item status. | REQ-API-04 |
| AC-API-06 | An invalid `category`, `status`, `page` or `pageSize` returns HTTP 400 with a problem-details JSON body that names the problem. | REQ-API-05 |
| AC-API-07 | POST, PUT, PATCH and DELETE to any `/api/v1/...` route return HTTP 405 and change no data. | REQ-API-06 |
| AC-API-08 | No API response contains borrower names, card numbers, loan data, fines or replacement cost. | REQ-API-07 |
| AC-API-09 | `GET /api/v1/items/by-code/BOK-1001` returns the item; a malformed code returns 400; an unknown code returns 404. | REQ-API-08 |
| AC-API-10 | `/openapi/v1.json` returns the API description and `/api-docs.html` displays it. | REQ-API-09 |
| AC-IMP-01 | When signed in as Admin, the Bulk Import page opens; Reception, Manager and anonymous users are refused (redirected to login or access denied). | REQ-IMP-01 |
| AC-IMP-02 | Uploading `valid_import.csv` shows 5 valid rows; confirming the import adds 5 items that appear in the catalogue. | REQ-IMP-02, 06, 07 |
| AC-IMP-03 | Uploading `mixed_import.csv` shows 3 valid, 5 invalid and 2 duplicate rows, each invalid or duplicate row with its row number and reason. Confirming imports only the 3 valid rows. | REQ-IMP-02, 03, 04, 06 |
| AC-IMP-04 | Uploading `empty.csv`, `header_only.csv`, `missing_columns.csv`, `unterminated_quote.csv`, a renamed non-CSV file, or a file over 512 KB shows a clear message, imports nothing and does not crash. | REQ-IMP-05 |
| AC-IMP-05 | Uploading the same valid file a second time reports every row as a duplicate and adds nothing. | REQ-IMP-04, 06 |
| AC-IMP-06 | The downloaded template opens in a spreadsheet and can be uploaded without errors. | REQ-IMP-08 |
| AC-ANA-01 | The Reports page shows the borrowing, fines and inventory reports with figures that agree with the Manager dashboard for the same data. | REQ-ANA-01, 02, 03 |
| AC-ANA-02 | Setting a From/To date changes the borrowing and fines figures; clearing the filter restores all data; From later than To is swapped with a message. | REQ-ANA-04 |
| AC-ANA-03 | Reception and anonymous users cannot open the Reports page or the export links. | REQ-ANA-05 |
| AC-EXP-01 | Each of the three reports downloads as a `.csv` file that opens in Excel with the summary and every table. | REQ-EXP-01, 03 |
| AC-EXP-02 | Each of the three reports downloads as a `.pdf` file that opens in a PDF viewer with the title, summary and every table. | REQ-EXP-02, 03 |
| AC-EXP-03 | A report for a date range with no data still exports to CSV and PDF, with "No data" messages. | REQ-EXP-03 |
| AC-GEN-01 | `dotnet build` finishes with no errors, and the public catalogue, login, loans, fines and manager dashboard still work. | REQ-GEN-01, 02 |

---

## 3. Traceability table

Status values: **Built and tested** = the code is written and the test was run and passed (see `04_Test_Plan_and_Evidence_Log.md`). **Not built** = left out of my part for the reason given.

| Requirement | Implemented in | Acceptance criteria | Test cases | Status |
|---|---|---|---|---|
| REQ-API-01 | `Controllers/Api/CatalogApiController.cs` (`GetItems`) | AC-API-01, 02 | TC-API-01, 02, 03 | Built and tested |
| REQ-API-02 | `CatalogApiController.GetItem` | AC-API-03 | TC-API-04, 05, 06 | Built and tested |
| REQ-API-03 | `CatalogApiController.GetCategories` | AC-API-04 | TC-API-07 | Built and tested |
| REQ-API-04 | `CatalogApiController.GetStatus` | AC-API-05 | TC-API-08 | Built and tested |
| REQ-API-05 | `CatalogApiController` (problem details), `[ApiController]` model validation | AC-API-06 | TC-API-09, 10 | Built and tested |
| REQ-API-06 | Only `[HttpGet]` actions exist; CORS policy allows GET/HEAD only (`Program.cs`) | AC-API-07 | TC-API-11 | Built and tested |
| REQ-API-07 | `Models/Api/ApiDtos.cs` (DTOs instead of entities) | AC-API-08 | TC-API-12 | Built and tested |
| REQ-API-08 | `CatalogApiController.GetItemByCode` | AC-API-09 | TC-API-13 | Built and tested |
| REQ-API-09 | `Program.cs` (`AddOpenApi`, `MapOpenApi`), `wwwroot/api-docs.html` | AC-API-10 | TC-API-14 | Built and tested |
| REQ-API-10 | `CatalogApiController.GetItems` (page, pageSize, filters) | AC-API-01, 02 | TC-API-02, 03 | Built and tested |
| REQ-IMP-01 | `Controllers/ImportController.cs` (`[Authorize(Roles = "Admin")]`) | AC-IMP-01 | TC-IMP-01 | Built and tested |
| REQ-IMP-02 | `Services/CsvImportService.cs` (`EvaluateRow`, data annotations) | AC-IMP-02, 03 | TC-IMP-02, 03 | Built and tested |
| REQ-IMP-03 | `CsvImportService`, `Views/Import/Index.cshtml` | AC-IMP-03 | TC-IMP-03 | Built and tested |
| REQ-IMP-04 | `CsvImportService` (existing and in-file duplicate checks) | AC-IMP-03, 05 | TC-IMP-03, 05 | Built and tested |
| REQ-IMP-05 | `CsvImportService`, `Services/CsvParser.cs`, `ImportController.Preview` | AC-IMP-04 | TC-IMP-04a to 04g | Built and tested |
| REQ-IMP-06 | `CsvImportService` (transaction in `ProcessAsync`) | AC-IMP-02, 03, 05 | TC-IMP-02, 03, 05 | Built and tested |
| REQ-IMP-07 | `ImportController.Preview` / `Confirm`, `Views/Import/Index.cshtml` | AC-IMP-02 | TC-IMP-02 | Built and tested |
| REQ-IMP-08 | `ImportController.Template`, `CsvImportService.BuildTemplate` | AC-IMP-06 | TC-IMP-06 | Built and tested |
| REQ-ANA-01 | `Services/ReportService.cs` (`BuildBorrowingAsync`) | AC-ANA-01 | TC-ANA-01 | Built and tested |
| REQ-ANA-02 | `ReportService.BuildFinesAsync` | AC-ANA-01 | TC-ANA-01 | Built and tested |
| REQ-ANA-03 | `ReportService.BuildInventoryAsync` | AC-ANA-01 | TC-ANA-01 | Built and tested |
| REQ-ANA-04 | `ReportsController.Index`, `ReportService.NormaliseRange` | AC-ANA-02 | TC-ANA-02 | Built and tested |
| REQ-ANA-05 | `ReportsController` (`[Authorize(Roles = "Manager,Admin")]`) | AC-ANA-03 | TC-ANA-03 | Built and tested |
| REQ-EXP-01 | `Services/ReportExporter.cs` (`ToCsv`), `Services/CsvWriter.cs` | AC-EXP-01 | TC-EXP-01 | Built and tested |
| REQ-EXP-02 | `ReportExporter.ToPdf` (QuestPDF) | AC-EXP-02 | TC-EXP-02 | Built and tested |
| REQ-EXP-03 | `CsvWriter.Escape` (formula guard), "No data" messages in `ReportExporter` | AC-EXP-03 | TC-EXP-03 | Built and tested |
| REQ-ANA-06 | Not built: it needs the multi-branch model, which is Taief's part (see the notes) | – | – | Not built |
| REQ-GEN-01 | Whole solution | AC-GEN-01 | TC-GEN-01 | Built and tested |
| REQ-GEN-02 | Uses `ApplicationDbContext`, `Item`/`Loan`/`Fine` entities and existing roles | AC-GEN-01 | TC-GEN-01 | Built and tested |


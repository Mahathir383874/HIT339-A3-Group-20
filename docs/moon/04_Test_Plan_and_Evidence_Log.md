# Moon – Test Plan and Evidence Log

**Name:** Mishkatuz Zannat Moon · **Student ID:** 385411 · **Unit:** HIT339 Group Project (library system)

This is the test plan for my three modules (the read-only API, the CSV importer, and the manager reports and export), and what happened when I ran each test.

I ran every test on the local site (localhost:7243) in a browser, using the seeded Admin, Reception and Manager accounts. The test data was the seeded database (13 items) plus the sample files in `docs/moon/sample-data/`. All 33 tests passed.

The **Evidence** column says where to find the screenshot in my portfolio: either a numbered Figure in the Test evidence section, or an Appendix Figure (A1 to A66) at the end.

## 1. API tests

| Test | Steps | Expected | Result | Evidence |
|---|---|---|---|---|
| TC-API-01 | `GET /api/v1/items` | 200, JSON with paging fields and items | Pass | Figure 1 |
| TC-API-02 | `GET /api/v1/items?category=Book&status=Available&pageSize=2` | 200, only available books, at most 2 per page, `totalPages` correct | Pass | Figure 2 |
| TC-API-03 | `GET /api/v1/items?search=hobbit` and `?search=zzzz` | First returns The Hobbit; second returns 200 with an empty `items` list | Pass | Appendix Figures A14 to A15 |
| TC-API-04 | `GET /api/v1/items/1` | 200, one item with type-specific fields | Pass | Appendix Figure A16 |
| TC-API-05 | `GET /api/v1/items/99999` | 404 problem-details JSON | Pass | Figure 3 |
| TC-API-06 | `GET /api/v1/items/0`, `/-5`, `/abc` | 400 each | Pass | Appendix Figures A17 to A18 |
| TC-API-07 | `GET /api/v1/categories` | 200, Book, Music, Toy with counts | Pass | Appendix Figure A19 |
| TC-API-08 | `GET /api/v1/status` | 200, status "Operational", counts for all four item statuses | Pass | Appendix Figure A20 |
| TC-API-09 | `GET /api/v1/items?category=Magazine` and `?status=Lost` | 400, message lists allowed values | Pass | Figure 4 |
| TC-API-10 | `GET /api/v1/items?page=0` and `?pageSize=500` | 400 each | Pass | Appendix Figures A21 to A22 |
| TC-API-11 | `DELETE /api/v1/items/1` and `POST /api/v1/items` | 405, catalogue unchanged | Pass: DELETE, POST and PUT all returned 405; item count unchanged (13) | Sent from the browser console, so there is no page screenshot |
| TC-API-12 | Read any item response | No replacement cost, loans, borrowers or fines present | Pass | Appendix Figure A16 |
| TC-API-13 | `GET /api/v1/items/by-code/BOK-1001`, `/by-code/xyz`, `/by-code/BOK-9999` | 200, 400, 404 | Pass | Appendix Figures A23 to A25 |
| TC-API-14 | Open `/openapi/v1.json` and `/api-docs.html` | JSON document loads; page lists the endpoints (needs internet for the page's style/script files) | Pass | Figure 5 and Appendix Figures A26 to A27 |

## 2. Importer tests

| Test | Steps | Expected | Result | Evidence |
|---|---|---|---|---|
| TC-IMP-01 | Open `/Import` as Admin, Reception, Manager and when signed out | Admin sees the page; others are refused or redirected to login | Pass | Appendix Figures A54 to A55 |
| TC-IMP-02 | Upload `valid_import.csv`, check the preview, press Import | 5 valid, 5 items added and visible in the catalogue | Pass: 5 valid, 5 imported | Figure 6 and Appendix Figures A56 to A58 |
| TC-IMP-03 | Upload `mixed_import.csv`, check the preview, press Import | 3 valid / 5 invalid / 2 duplicate with reasons; only 3 items added | Pass: preview 3/5/2, then Import added 3 | Figure 7 and Appendix Figures A59 to A60 |
| TC-IMP-04a | Upload `empty.csv` | Message asking for a file; nothing imported | Pass | Appendix Figure A61 |
| TC-IMP-04b | Upload `header_only.csv` | File error: no data rows | Pass | Appendix Figure A62 |
| TC-IMP-04c | Upload `missing_columns.csv` | File error: required columns missing | Pass | Appendix Figure A63 |
| TC-IMP-04d | Upload `unterminated_quote.csv` | File error: quote never closed | Pass | Appendix Figure A64 |
| TC-IMP-04e | Rename a `.xlsx` to `.csv` and upload | File error: binary data | Pass | Appendix Figure A65 |
| TC-IMP-04f | Upload a `.txt` file | Message: only .csv files | Pass | Appendix Figure A66 |
| TC-IMP-04g | Upload a CSV larger than 512 KB | Message: file too large | Pass | Figure 8 |
| TC-IMP-05 | Upload `valid_import.csv` again after TC-IMP-02 | 5 duplicates, 0 valid, nothing added | Pass: 5 duplicates, 0 valid | No screenshot |
| TC-IMP-06 | Download the template, upload it unchanged | 3 valid rows (codes `BOK-9901`, `MUS-9901`, `TOY-9901`); do not confirm unless you want these sample items | Pass: 3 valid (BOK-9901, MUS-9901, TOY-9901), not imported | No screenshot |

## 3. Analytics and export tests

| Test | Steps | Expected | Result | Evidence |
|---|---|---|---|---|
| TC-ANA-01 | Sign in as Manager, open Reports & Export; compare with the Manager dashboard | Three reports shown; loan, overdue, fine and inventory totals agree with the dashboard | Pass | Figure 9 and Appendix Figures A1 to A9 |
| TC-ANA-02 | Set From/To to a narrow range, then Clear; then set From later than To | Figures change then restore; dates are swapped with a message | Pass | Figure 10 and Appendix Figures A10 to A12 |
| TC-ANA-03 | Open `/Reports` as Reception and signed out | Access denied / redirect to login | Pass | Figure 14 and Appendix Figure A13 |
| TC-EXP-01 | Export CSV for Borrowing, Fines, Inventory; open in Excel | Three files open with summary and tables, no garbled characters | Pass | Figure 11 and Appendix Figures A28 to A32 |
| TC-EXP-02 | Export PDF for Borrowing, Fines, Inventory; open in a viewer | Three files open with title, summary, tables and page numbers | Pass: PDF opened and rendered, with title, summary, tables and page numbers | Figure 12 and Appendix Figures A33 to A37 |
| TC-EXP-03 | Filter to a date range with no data (for example 2000-01-01 to 2000-01-02); export CSV and PDF of Borrowing and Fines | Files still open, with "No ... in this period" messages | Pass: CSV shows the 'No ... in this period' messages; PDFs generated | Figure 13 and Appendix Figures A38 to A46 |

## 4. General

| Test | Steps | Expected | Result | Evidence |
|---|---|---|---|---|
| TC-GEN-01 | `dotnet build`; then use the public catalogue, login, issue/return a loan, Manager dashboard | Build succeeds; existing features unchanged | Pass: build, issue and return a loan, Manager dashboard | Figures 15, 16, 17 and Appendix Figures A47 to A53 |

## 5. Task hours

The hours are estimates. I didn't track my time as I worked, so I set them equal to what I planned.

| Task ID | Task | Planned hours | Actual hours (estimate) |
|---|---|---|---|
| MOON-01 | Read-only API: items, item by id/code, categories, status, error handling | 8 | 8 |
| MOON-02 | OpenAPI document and API documentation page | 2 | 2 |
| MOON-03 | CSV importer: upload, validation, preview, confirm | 10 | 10 |
| MOON-04 | Reports: borrowing, fines and inventory with date filter | 8 | 8 |
| MOON-05 | CSV and PDF export | 6 | 6 |
| MOON-06 | Requirements list, acceptance criteria, traceability table | 4 | 4 |
| MOON-07 | API, importer and reporting notes | 3 | 3 |
| MOON-08 | Testing, screenshots and evidence | 5 | 5 |
| MOON-09 | Project plan and integration diagrams (with Taief) | 8 | 8 |
| MOON-10 | Meetings, merge fixes and final review | 6 | 6 |


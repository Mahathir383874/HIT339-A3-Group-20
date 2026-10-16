# Bibliotheca – Library Management System

**Bibliotheca** is a modern, responsive, and secure Library Management System built with **ASP.NET Core MVC (.NET 10)**, **Entity Framework Core**, and **ASP.NET Core Identity**.

---

## Quick Start & Running Locally

**Full instructions:** see [`INSTALL.md`](INSTALL.md) (requirements, branch selection, database setup, troubleshooting).

Fastest way (after `git clone` and `cd` into the repository):

```powershell
# Windows
.\setup\setup.cmd -Branch main
```
```bash
# Linux / macOS
bash setup/setup.sh --branch main
```

Or manually:

```bash
cd HIT-339_A2_Nawshin
dotnet run --launch-profile http
```

Then open **http://localhost:5226** (catalogue) or **http://localhost:5226/Kiosk** (self-service kiosk).

> **Zero-Configuration Database:**
> The application uses an embedded **SQLite** database (`bibliotheca.db`, next to the project). On first launch it is created automatically and seeded with staff accounts, diverse items (Books, Music, Toys), borrower cards, active & overdue loans, and fine records. Delete `bibliotheca.db*` (or run the setup script with `-ResetDatabase` / `--reset-db`) to start again from clean seed data.

---

## Navigating the Modules

| Module | Where to find it | Owner | How to test it |
| :--- | :--- | :--- | :--- |
| **Public Self-Service Kiosk** | `/Kiosk` · navbar → *Self-Service Kiosk* | Achintha | See [Self-Service Kiosk](#self-service-kiosk) below and [`docs/kiosk-test-plan.md`](docs/kiosk-test-plan.md) |
| Public read-only REST API | *(to be added)* | Moon | *(to be added)* |
| CSV / external data importer | *(to be added)* | Moon | *(to be added)* |
| Manager analytics & CSV/PDF export | *(to be added)* | Moon | *(to be added)* |
| Reservations & waitlist | *(to be added)* | Tanisha | *(to be added)* |
| Notifications log | *(to be added)* | Tanisha | *(to be added)* |
| Multi-branch inventory & transfers | *(to be added)* | Taief | *(to be added — include how to seed multi-branch data)* |

### Self-Service Kiosk

A touch-friendly UI for in-library kiosk stations, separate from the staff pages (own layout, no staff menus).

1. Open `http://localhost:5226/Kiosk`.
2. Scan or type a library card (`CRD-1001` … `CRD-1004`) using a USB barcode scanner, the on-screen keypad, the keyboard, or **Scan with camera**.
3. **My account** — items on loan with due dates, overdue items with the fine building up, unpaid fines, and reservations (once the reservation module is connected).
4. **Search catalogue** — by title, author/artist or category, with an *Available now only* filter. Works without a card too.
5. **Borrow an item** — scan the item's code (e.g. `BOK-1001`) or tap **Borrow** in search results, check the due date, then **Confirm and borrow**.
6. **End session** — or walk away: after 90 s of inactivity the kiosk warns, then signs the patron out automatically. Pages are sent with `no-store` so the browser Back button cannot show the previous patron.

| Test card | Patron | What it demonstrates |
| :--- | :--- | :--- |
| `CRD-1001` | David Smith | Overdue loan → fine so far, self-service borrowing paused |
| `CRD-1002` | Emma Watson | Normal account with one loan; can borrow |
| `CRD-1003` | Michael Brown | No current loans |
| `CRD-1004` | Sophia Davis | New patron |

Kiosk rules (loan length, item limit, fine limit, idle timeout, location name) are in `appsettings.json` → `"Kiosk"`; see [`INSTALL.md`](INSTALL.md#5-per-location-settings).
Staff can print QR test cards from **Desk Circulation → Kiosk Test Cards (QR)** (Reception/Admin).

**Code map**

| File | Purpose |
| :--- | :--- |
| `Controllers/KioskController.cs` | Kiosk routes, patron session, idle/lockout checks |
| `Services/CirculationService.cs` | Shared borrowing rules used by **both** the Reception desk and the kiosk |
| `Services/CatalogSearchExtensions.cs` | Shared keyword search used by the public catalogue and the kiosk |
| `Services/KioskSupport.cs` | `KioskOptions` settings and the `IReservationSummaryProvider` hook for the reservation module |
| `Models/ViewModels/KioskViewModels.cs` | Kiosk view models |
| `Views/Kiosk/*`, `Views/Shared/_KioskLayout.cshtml` | Kiosk pages and layout |
| `wwwroot/css/kiosk.css`, `wwwroot/js/kiosk.js` | Touch styling, keypad, idle timer, camera scanning |

**Connecting reservations:** implement `IReservationSummaryProvider` in the reservation module and replace `NoReservationSummaryProvider` in the one registration line in `Program.cs`. The kiosk account page will then list the patron's holds and queue positions.

---

## Default Staff Accounts

The database is pre-seeded with default staff accounts for testing each role:

| Role | Email | Password | Primary Capabilities |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@bibliotheca.com` | `Admin@123` | Full CRUD operations for Books, Music, and Toys; Accession Code validation; Status management |
| **Reception** | `reception@bibliotheca.com` | `Reception@123` | CRUD Borrower patron records; Issue items; Return items with automated late fine calculations; Fine desk collection |
| **Manager** | `manager@bibliotheca.com` | `Manager@123` | Interactive visual statistics dashboard (Borrowing trends, Item status breakdown, Fine revenue vs damage costs) |
| **Public** | *(No login required)* | *(N/A)* | Responsive mobile-friendly search portal; Keyword & category filtering; Item details & live availability |

---

## Architectural Highlights

### 1. Object-Oriented Domain Model (TPH Inheritance)
- **Base Superclass `Item`**: Encapsulates common attributes: `Name`, `Description`, `LibraryCode` (Regex validated `^[A-Z]{3}-\d{4}$`), `Status` (`Available`, `Borrowed`, `Damaged`, `Destroyed`), `ReplacementCost`, and `ImageUrl`.
- **Derived Classes**:
  - `Book`: Extends `Item` with `Author`, `Genre`, `ISBN`, and `PageCount`.
  - `Music`: Extends `Item` with `Artist`, `ReleaseYear`, `RecordLabel`, and `DurationMinutes`.
  - `Toy`: Extends `Item` with `ToyType`, `RecommendedAge`, `Manufacturer`, and `SafetyWarning`.
- **Table-Per-Hierarchy (TPH)**: Efficient single-table database schema with discriminator column configured in `ApplicationDbContext`.

### 2. Loan Processing & Overdue Fine Engine
- Standard 14-day default loan periods (customizable during issue).
- Automated overdue detection and penalty calculation ($1.00 per day overdue).
- Support for immediate on-the-spot fine collection or debt tracking on patron accounts.

### 3. Manager Analytics Dashboard
- Interactive **Chart.js** data visualizations:
  - Inventory distribution (Available vs Borrowed vs Damaged vs Destroyed).
  - Circulation volume by media category (Books, Music, Toys).
- Financial ledger comparing collected fine revenue against estimated damaged item replacement liability.

### 4. Public Mobile-Friendly Search Portal
- Live search filtering by title, accession code, author/artist, category, genre, and availability.
- Accessible from any smartphone or tablet viewport.

---

## Documentation Files
- **`PROGRAMMERS_LOG.md`**: Complete chronological development diary recorded step-by-step during project implementation.
- **`AI_USAGE_LOG.md`**: Formal AI attribution disclosing tools, dates, prompts, and outputs according to CDU academic integrity guidelines.

# Bibliotheca – Library Management System

**Bibliotheca** is a modern, responsive, and secure Library Management System built with **ASP.NET Core MVC (.NET 10)**, **Entity Framework Core**, and **ASP.NET Core Identity**.

---

## Quick Start & Running Locally

1. Open a terminal and navigate to the project directory:
   ```bash
   cd Bibliotheca
   ```
2. Run the application:
   ```bash
   dotnet run
   ```
3. Open your browser and navigate to:
   ```
   http://localhost:5128 (or the port displayed in the console)
   ```

> **Zero-Configuration Setup:**  
> The application uses an embedded **SQLite** database (`bibliotheca.db`). Upon first launch, the database is automatically created, migrated, and seeded with staff accounts, diverse items (Books, Music, Toys), borrower cards, active & overdue loans, and fine records.

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

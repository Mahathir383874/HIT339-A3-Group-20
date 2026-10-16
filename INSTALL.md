# Bibliotheca LMS — Installation and Running Instructions

These instructions take a clean machine to a working Bibliotheca site for **any library location**.
Every location follows exactly the same steps. The only thing that changes is the **location branch** you choose in step 3.

> Prepared by Achintha Fernando (S390909) — installation guide and kiosk module owner.
> Tested on: *see the VM test record in section 9.*

---

## 1. What you will end up with

| Page | Address (default) | Who uses it |
| :--- | :--- | :--- |
| Public catalogue search | `http://localhost:5226/` | Patrons on phones / PCs |
| **Self-service kiosk** | `http://localhost:5226/Kiosk` | Patrons at an in-library kiosk |
| Staff login | `http://localhost:5226/Account/Login` | Admin, Reception, Manager |

The database is a single **SQLite** file (`bibliotheca.db`) that is created and filled with demo data automatically the first time the site starts. No database server needs to be installed.

---

## 2. Requirements and software list

| Software | Version | Needed for | How to install (Windows) | How to install (Ubuntu 24.04) |
| :--- | :--- | :--- | :--- | :--- |
| **.NET SDK** | **10.0** (10.0.100 or later) | Building and running the site | `winget install --id Microsoft.DotNet.SDK.10 -e` or download from <https://dotnet.microsoft.com/download/dotnet/10.0> | `sudo apt install dotnet-sdk-10.0` |
| **Git** | 2.23 or later | Downloading the code and choosing a branch | `winget install --id Git.Git -e` | `sudo apt install git` |
| Web browser | Current Edge, Chrome or Firefox | Using the site | Pre-installed | Pre-installed |
| Visual Studio 2026 *or* VS Code + C# Dev Kit | Optional | Editing / debugging only | — | — |

**Machine:** Windows 10/11 or Windows Server 2019+, Ubuntu 22.04+, or macOS 13+. 2 GB free RAM, 1 GB free disk.

**Network:** Internet access is needed the first time (NuGet packages are downloaded during restore). The icons and catalogue cover images also load from the internet; the site still works without them.

> After installing the .NET SDK or Git, **close and reopen** your terminal so the new commands are found.
> Check with: `dotnet --list-sdks` (must show a line starting with `10.`) and `git --version`.

---

## 3. Quick install (recommended): one script

The `setup` folder contains a script that does every step in section 4 for you and stops with a clear message if anything is missing.

### Windows (PowerShell or Command Prompt)

```powershell
# 1. Download the code (first time only)
git clone <REPO_URL> "$HOME\Bibliotheca"
cd "$HOME\Bibliotheca"

# 2. Install and run your location (replace the branch / name for your library)
.\setup\setup.cmd -Branch main -Location "Darwin City Library"
```

### Linux / macOS

```bash
git clone <REPO_URL> ~/Bibliotheca
cd ~/Bibliotheca
bash setup/setup.sh --branch main --location "Darwin City Library"
```

When you see `Now listening on: http://localhost:5226`, open **http://localhost:5226/Kiosk** in a browser. Press **Ctrl + C** in the terminal to stop the site.

### Script options

| Windows (`setup.cmd` / `setup.ps1`) | Linux / macOS (`setup.sh`) | Meaning |
| :--- | :--- | :--- |
| `-RepoUrl <url>` | `--repo <url>` | Clone from this URL (only needed if you have not cloned yet) |
| `-Branch <name>` | `--branch <name>` | Location branch to run (default `main`) |
| `-InstallDir <path>` | `--dir <path>` | Where to clone (default `~/Bibliotheca`) |
| `-Location "<name>"` | `--location "<name>"` | Library name shown on the kiosk header |
| `-Port <n>` | `--port <n>` | Use another port (default `5226`) |
| `-ResetDatabase` | `--reset-db` | Delete the local database and re-seed fresh demo data |
| `-NoRun` | `--no-run` | Install and build only, do not start the site |

---

## 4. Manual install (step by step)

Use this if you prefer to type each command yourself. All commands are the same on Windows (PowerShell) and Linux/macOS unless shown otherwise.

### Step 1 — Download the project

```bash
git clone <REPO_URL> Bibliotheca
cd Bibliotheca
```

### Step 2 — See the available branches

```bash
git fetch --all
git branch -r
```

### Step 3 — Choose your location branch

```bash
git switch <branch-name>        # e.g.  git switch main
git pull --ff-only
```

| Library location | Branch | Kiosk `-Location` name |
| :--- | :--- | :--- |
| All locations (integrated build) | `main` | `Bibliotheca Library` |
| *(team to confirm final location branches, e.g. `location/darwin-city`, `location/palmerston`)* | | |

> Once the multi-branch inventory module is merged, the database records which physical branch holds each item, so every location can run the same code from `main`. A separate location branch is only needed if a location must have its own settings committed (for example its own `appsettings.json` values). Update this table when the team confirms the final branch names.

### Step 4 — Restore packages and build

```bash
dotnet restore HIT-339_A2_Nawshin/Bibliotheca.csproj
dotnet build   HIT-339_A2_Nawshin/Bibliotheca.csproj
```

Expected result: `Build succeeded.` with `0 Error(s)`.

### Step 5 — Database setup

Nothing to install. On start-up `Data/DbInitializer.cs`:

1. creates `bibliotheca.db` next to the project if it does not exist, and
2. seeds the three staff accounts, 13 catalogue items (books, music, toys), 4 borrowers and sample loans/fines.

**Start with a clean database** (recommended on a fresh VM, after pulling new features, or before a demo):

```powershell
# Windows
Remove-Item HIT-339_A2_Nawshin\bibliotheca.db* -ErrorAction SilentlyContinue
```
```bash
# Linux / macOS
rm -f HIT-339_A2_Nawshin/bibliotheca.db*
```

> **Migrations:** the current build creates the schema with `EnsureCreated()`, so there are no migration commands to run. If the team later adds an EF Core `Migrations` folder, run `dotnet tool install --global dotnet-ef` once and then `dotnet ef database update --project HIT-339_A2_Nawshin/Bibliotheca.csproj`. The setup scripts detect a `Migrations` folder and do this automatically.

### Step 6 — Run the site

Run from **inside the project folder** so the database file is created there:

```bash
cd HIT-339_A2_Nawshin
dotnet run --launch-profile http
```

To set the location name shown on the kiosk, add it after `--`:

```bash
dotnet run --launch-profile http -- --Kiosk:BranchName="Palmerston Library"
```

Wait for `Now listening on: http://localhost:5226`, then open the addresses in section 1.

### Step 7 — Log in (staff)

| Role | Email | Password |
| :--- | :--- | :--- |
| Admin | `admin@bibliotheca.com` | `Admin@123` |
| Reception | `reception@bibliotheca.com` | `Reception@123` |
| Manager | `manager@bibliotheca.com` | `Manager@123` |

Patrons do **not** log in. At the kiosk they scan or type their library card number (see section 6).

---

## 5. Per-location settings

Kiosk settings live in `HIT-339_A2_Nawshin/appsettings.json` under `"Kiosk"`. Any value can be overridden for one location without editing files, either on the command line (`--Kiosk:Setting=value`) or with an environment variable (`Kiosk__Setting=value`).

| Setting | Default | What it does |
| :--- | :--- | :--- |
| `BranchName` | `Bibliotheca Library` | Location name shown in the kiosk header |
| `IdleTimeoutSeconds` | `90` | Inactivity before the "Are you still there?" warning |
| `IdleWarningSeconds` | `15` | Countdown on the warning before the session ends automatically |
| `LoanDays` | `14` | Loan length for kiosk checkouts |
| `MaxActiveLoans` | `5` | Kiosk refuses checkout once a patron has this many items |
| `BlockWhenOverdue` | `true` | Kiosk refuses checkout while the patron has an overdue item |
| `MaxUnpaidFines` | `10.00` | Kiosk refuses checkout when unpaid fines are above this amount |
| `MaxFailedCardAttempts` | `5` | Wrong card numbers allowed before lookups pause |
| `LockoutSeconds` | `60` | How long lookups pause after too many wrong cards |
| `EnableCameraScan` | `true` | Shows the "Scan with camera" button |

Example (Windows, one location):

```powershell
$env:Kiosk__BranchName = "Casuarina Library"
$env:Kiosk__IdleTimeoutSeconds = "120"
dotnet run --launch-profile http
```

---

## 6. Setting up a physical kiosk station (optional)

1. Run the site on the kiosk PC (or on a library server and use its address instead of `localhost`).
2. Open the kiosk full-screen with no address bar:
   - **Edge:** `msedge --kiosk http://localhost:5226/Kiosk --edge-kiosk-type=public-browsing`
   - **Chrome:** `chrome --kiosk http://localhost:5226/Kiosk`
3. **USB barcode scanner:** set it to *keyboard (HID) mode* with an *Enter* suffix. The card and item boxes are focused automatically, so a scan types the code and submits it.
4. **Webcam scanning:** the "Scan with camera" button reads QR codes and barcodes. Browsers only allow the camera on `http://localhost` or an `https://` address.
5. **Test cards:** log in as Reception → *Desk Circulation* → *Kiosk Test Cards (QR)* to print QR codes for every borrower card and available item.

---

## 7. Check that the install worked

| # | Check | Expected result |
| :-: | :--- | :--- |
| 1 | `dotnet build` | `Build succeeded`, 0 errors |
| 2 | Open `http://localhost:5226/` | Public catalogue shows 13 items |
| 3 | Open `http://localhost:5226/Kiosk` | Kiosk welcome screen with keypad; your location name in the header |
| 4 | Enter `CRD-1002` on the kiosk | "Hello, Emma" account page with 1 item on loan |
| 5 | Kiosk → Search → "Toys & games" → **Borrow** → **Confirm** | "You're all set" receipt with a due date 14 days away |
| 6 | Tap **End session** | Back to the welcome screen; browser Back does not show Emma's account |
| 7 | Log in as `reception@bibliotheca.com` | Desk Circulation menu appears; the kiosk loan is listed under *Active Loans* |

---

## 8. Troubleshooting

| Problem | Fix |
| :--- | :--- |
| `dotnet` is not recognised / `No .NET SDKs were found` | Install the .NET 10 SDK (section 2), then open a **new** terminal. |
| `The current .NET SDK does not support targeting .NET 10.0` | An older SDK is installed. Install .NET 10 and check `dotnet --list-sdks`. |
| Restore fails: `Unable to load the service index for source https://api.nuget.org` | No internet or a proxy is blocking NuGet. Connect to the internet (or set your proxy) and run `dotnet restore` again. |
| `running scripts is disabled on this system` | Use `setup\setup.cmd` instead of `setup.ps1` (it bypasses the policy for this run only). |
| `Failed to bind to address http://127.0.0.1:5226: address already in use` | Another copy is running. Stop it (Ctrl + C) or use another port: `setup.cmd -Port 5300`. |
| `SQLite Error 1: 'no such table'` or `'no such column'` after pulling new code | The database was made by an older version. Delete `bibliotheca.db*` (section 4, step 5) and start again. |
| `database is locked` | Two copies of the site are using the same database. Stop all copies and start one. |
| Warning `Failed to determine the https port for redirect` | Harmless when using the `http` profile. |
| Icons or cover pictures are missing | The PC has no internet access; everything still works. |
| Kiosk "Scan with camera" says the camera is unavailable | Use `http://localhost` or HTTPS, allow camera permission in the browser, or type / USB-scan the code instead. |
| Kiosk says "Too many unsuccessful attempts" | Wait 60 seconds (setting `LockoutSeconds`). |
| `Branch '<name>' does not exist` from the setup script | Run `git branch -r` and use one of the listed names. |

---

## 9. VM test record (evidence)

Fill in one row each time the instructions are tested on the virtual machine and save a screenshot of each result.

| Date | Tester | VM OS / .NET SDK | Branch | Steps followed | Result | Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| | | | | Section 3 (script) | | |
| | | | | Section 4 (manual) | | |

# Bibliotheca LMS — Installation and Running Instructions

These instructions take a clean machine to a working Bibliotheca site for **any library location**.
Every location follows exactly the same steps. The only thing that changes is the **location branch** you choose in step 3.

> Prepared by Achintha Fernando (S390909) — installation guide and kiosk module owner.
> Tested on the CDU IT Desktop (Windows 10 virtual desktop) on 16/10/2026 — see the VM test record in section 9 and the screenshots in [`docs/vm-test-evidence.md`](docs/vm-test-evidence.md).

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
git clone https://github.com/Mahathir383874/HIT339-A3-Group-20.git "$HOME\Bibliotheca"
cd "$HOME\Bibliotheca"

# 2. Install and run your location (replace the branch / name for your library)
.\setup\setup.cmd -Branch main -Location "Darwin City Library"
```

### Linux / macOS

```bash
git clone https://github.com/Mahathir383874/HIT339-A3-Group-20.git ~/Bibliotheca
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
git clone https://github.com/Mahathir383874/HIT339-A3-Group-20.git Bibliotheca
cd Bibliotheca
```

> **If `git clone` hangs or times out** (this happens on the CDU IT Desktop, where the network blocks Git): open
> `https://github.com/Mahathir383874/HIT339-A3-Group-20/archive/refs/heads/main.zip` in the browser, right-click the downloaded file → **Extract All…** to your user folder, then continue from **Step 5** inside the extracted `HIT339-A3-Group-20-main\HIT-339_A2_Nawshin` folder. The setup script needs Git, so use these manual steps instead.

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
| `git clone` stays on `Cloning into…` and then says `Operation timed out after 300000 milliseconds` | The network is blocking Git (seen on the CDU IT Desktop). Download the ZIP from GitHub instead and follow section 4 from Step 5 (see the note under Step 1). |
| Setup script stops with `You have uncommitted changes` when run a second time | The database and build files are currently committed to the repository, and running the site changes them. Run `git checkout -- .` and try again, or use the manual steps in section 4. |
| Kiosk says "We couldn't find that library card" for a real card | Check the card was typed with a **dash**, e.g. `CRD-1002`, not `CRD 1002`. The on-screen **CRD-** key types the prefix and dash for you. |
| Yellow `NU1901`/`NU1902`/`NU1903` warnings during restore/build | Security advisories for packages inherited from the original project. They do not stop the build or the site; listed for the team's final package update. |

---

## 9. VM test record (evidence)

Fill in one row each time the instructions are tested on the virtual machine and save a screenshot of each result.
Screenshots are in [`docs/evidence/vm-test/`](docs/evidence/vm-test/) and explained step by step in [`docs/vm-test-evidence.md`](docs/vm-test-evidence.md).

| Date | Tester | VM OS / .NET SDK | Branch | Steps followed | Result | Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 16/10/2026 | Achintha Fernando (S390909) | CDU IT Desktop, Windows 10 / .NET SDK 10.0.302 (pre-installed), Git 2.31.1 | main | Section 3 (script): `git clone` of the repository | **Fail – blocked by network.** `git clone` timed out after 300 s with 0 bytes received, so the setup script could not be run. Guide updated: ZIP download alternative (section 4, Step 1) and new troubleshooting row. | vm-01 – vm-04 |
| 16/10/2026 | Achintha Fernando (S390909) | CDU IT Desktop, Windows 10 / .NET SDK 10.0.302 | main (ZIP download) | Section 4 (manual): ZIP download → delete old database → `dotnet restore` → `dotnet build` → `dotnet run --launch-profile http -- --Kiosk:BranchName="Darwin City Library"` | **Pass.** Restore succeeded (18.8 s) and build succeeded (19.3 s) with 4 package-advisory warnings; database created and seeded automatically; site listening on `http://localhost:5226`. Checks in section 7: catalogue shows 13 items (8 available) ✔; kiosk opens with "Darwin City Library" in the header ✔; `CRD-1002` opens Emma's account ✔; borrowed Catan Board Game, receipt due 30 Oct 2026 and "2 items on loan" ✔; after End session the kiosk shows no patron signed in and Catan shows "On loan" ✔; Reception → Active Loans lists the kiosk loan (TOY-3002, Emma Watson, due 30 Oct 2026) ✔. | vm-05 – vm-14 |

**Findings from the VM test**

1. Git is blocked on the CDU IT Desktop, so the one-step setup script cannot clone the repository there. The ZIP + manual route worked first time. *Action:* ZIP instructions and a troubleshooting row added to this guide.
2. Typing `CRD 1002` (space instead of dash) was correctly rejected with a clear message. A patron could easily make the same mistake. *Action:* troubleshooting row added; accepting spaces in card and item codes is suggested as a kiosk improvement.
3. The database (`bibliotheca.db*`) and build folders (`.vs`, `bin`, `obj`) are committed to the repository, which blocks re-running the setup script and causes conflicts between members. *Action:* reported to Taief for the final cleanup.
4. Not yet captured on the VM: the browser **Back** button check after End session (section 7, check 6). To be repeated and screenshotted.

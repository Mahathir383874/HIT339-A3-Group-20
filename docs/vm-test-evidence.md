# Installation Test on the Virtual Machine — Evidence

**Tester:** Achintha Fernando (S390909) · **Date:** 16/10/2026 · **Machine:** CDU IT Desktop (Windows 10 virtual desktop)
**Guide tested:** [`INSTALL.md`](../INSTALL.md) · **Repository:** <https://github.com/Mahathir383874/HIT339-A3-Group-20> (branch `main`)

Summary: the one-step setup script could not be used because the IT Desktop network blocks Git. The project was downloaded as a ZIP from GitHub and installed with the manual steps (section 4), which worked first time. All checks in section 7 passed except the browser **Back** button check, which was not screenshotted and will be repeated.

| # | Step | Result | Screenshot |
| :-: | :--- | :--- | :--- |
| 1 | Log in to the CDU IT Desktop | Clean Windows 10 virtual desktop | [vm-01](evidence/vm-test/vm-01-it-desktop.png) |
| 2 | `dotnet --list-sdks` and `git --version` | .NET SDK **10.0.302** and Git **2.31.1** already installed — no installation needed | [vm-02](evidence/vm-test/vm-02-sdk-git-check.png) |
| 3 | `git clone https://github.com/Mahathir383874/HIT339-A3-Group-20.git` | Stayed on "Cloning into…" | [vm-03](evidence/vm-test/vm-03-git-clone-started.png) |
| 4 | Retried with `git clone --depth 1 --progress …` | **Failed:** "Operation timed out after 300002 milliseconds with 0 out of 0 bytes received" — Git is blocked on this network | [vm-04](evidence/vm-test/vm-04-git-clone-timeout.png) |
| 5 | Downloaded `main.zip` from GitHub in the browser and extracted it to `C:\Users\S390909` | Folder `HIT339-A3-Group-20-main` created | [vm-05](evidence/vm-test/vm-05-zip-extracted.png) |
| 6 | `cd …\HIT-339_A2_Nawshin`, `Remove-Item bibliotheca.db*`, `dotnet restore`, `dotnet build` | **Restore succeeded** (18.8 s), **Build succeeded** (19.3 s), 4 package-advisory warnings (NU1901–NU1903) | [vm-06](evidence/vm-test/vm-06-restore-build-succeeded.png) |
| 7 | `dotnet run --launch-profile http -- --Kiosk:BranchName="Darwin City Library"` | Database created and seeded; **Now listening on: http://localhost:5226** | [vm-07](evidence/vm-test/vm-07-app-running.png) |
| 8 | Open `http://localhost:5226/` | Public catalogue: **13 items, 8 available** ✔ | [vm-08](evidence/vm-test/vm-08-catalogue-13-items.png) |
| 9 | Open `http://localhost:5226/Kiosk` | Kiosk welcome screen with "**Darwin City Library** · Self-Service" in the header ✔ | [vm-09](evidence/vm-test/vm-09-kiosk-welcome.png) |
| 10 | Typed `CRD 1002` (with a space) | Correctly rejected: "We couldn't find that library card…" (also covers kiosk test K-03) | [vm-10](evidence/vm-test/vm-10-kiosk-invalid-card.png) |
| 11 | Entered `CRD-1002` | "Hello, Emma", card shown as •••• 1002, 1 item on loan (*Kind of Blue*, due 27 Oct), $0.00 fines ✔ | [vm-11](evidence/vm-test/vm-11-kiosk-account-emma.png) |
| 12 | Search catalogue → Toys & games → Borrow *Catan Board Game* → Confirm and borrow | Receipt: "You're all set, Emma!", TOY-3002, return by **Friday 30 October 2026**, "You now have 2 items on loan" ✔ | [vm-12](evidence/vm-test/vm-12-kiosk-receipt-catan.png) |
| 13 | End session, then search for "Catan" | No patron signed in (header has no name, button says "Scan card to borrow"); Catan now shows **On loan** ✔ | [vm-13](evidence/vm-test/vm-13-kiosk-after-end-session.png) |
| 14 | Staff login as Reception → Desk Circulation → Active Loans | Kiosk loan listed: **TOY-3002 Catan Board Game – Emma Watson – issued 16 Oct 2026 – due 30 Oct 2026** ✔ | [vm-14](evidence/vm-test/vm-14-reception-active-loans.png) |

## Findings and actions

| Finding | Action taken |
| :--- | :--- |
| Git is blocked on the CDU IT Desktop, so `git clone` and the setup script cannot be used there. | Added ZIP-download instructions to `INSTALL.md` (section 4, Step 1) and a troubleshooting row. |
| `CRD 1002` with a space is rejected — an easy mistake for patrons. | Troubleshooting row added; suggested kiosk improvement: accept spaces in card/item codes. |
| Database and build folders are committed to the repository, so re-running the setup script reports "uncommitted changes". | Troubleshooting row added; reported to Taief for removal in the final cleanup. |
| Package advisory warnings NU1901–NU1903 during restore. | Do not affect the build; listed for the team's final package update. |
| Back-button check (section 7, check 6) not screenshotted. | To be repeated on the VM. |

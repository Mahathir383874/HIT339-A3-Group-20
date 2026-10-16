# Self-Service Kiosk — Test Plan and Evidence

Owner: Achintha Fernando (S390909) · Module: Public Self-Service Kiosk (`/Kiosk`)

Start from a clean database (`setup.cmd -ResetDatabase` or delete `bibliotheca.db*`) so the seeded data below is in place.
Record the actual result and a screenshot file name for each test. Record **actual** hours in your task log as you go.

## Test runs

| Run | Date | Machine and method | Tests covered |
| :-- | :-- | :-- | :-- |
| 1 | 16/10/2026 | CDU IT Desktop (Windows 10 virtual desktop), manual, during the installation test | K-01, K-03, K-08, K-16, K-24 (partial), K-13 (partial) – screenshots in `docs/evidence/vm-test/` |
| 2 | 16/10/2026, 11:29–11:53 | Achintha's laptop, Claude desktop app's built-in browser, AI-assisted | All 33 tests (its screenshots were replaced by run 3) |
| 3 | 16/10/2026, 12:20–12:48 | Achintha's laptop (Windows 11, ARM64); site run from Visual Studio at `https://localhost:7243` with a fresh database. **AI-assisted:** the tests were carried out in Achintha's own **Google Chrome** (desktop layout, 1280 px wide), driven by Claude through the Claude in Chrome extension under Achintha's supervision. Each screenshot is taken from that Chrome tab with the whole page fitted in; capture times are listed under *Screenshot times* below. | All 33 tests – screenshots in `docs/evidence/kiosk-tests/` |

**Screenshot times (run 3, 16/10/2026):** K-01 12:21:04 · K-02 12:21:20 · K-03 12:21:37 · K-04/K-08 12:21:56 · K-05 12:22:29 · K-33 12:23:35 · K-06 12:24:54–12:25:11 · K-07 12:25:57–12:27:23 · K-09 12:27:37 · K-21 12:28:04 · K-11 12:28:22 · K-12 12:28:45 · K-14 12:28:56 · K-24 12:29:08 · K-25 12:29:30 · K-15 12:29:57 · K-13 12:30:26 · K-16 12:30:56–12:33:45 · K-17 12:32:14 · K-18 12:32:26 · K-19 12:32:39 · K-20 12:32:51 · K-23 12:33:21 · K-10 12:36:14 · K-22 12:37:07 · K-26 12:39:03–12:39:33 · K-27 12:41:47 · K-28 12:44:47 · K-29 12:46:20–12:46:37 · K-30 12:46:52–12:47:06 · K-31 12:47:20 · K-32 12:47:48–12:48:34

**Run 3 summary:** 32 passed, 1 partial (K-31 camera scanning needs a person with a webcam), 0 failed.

**Findings**

1. **Date shown differently on staff and kiosk pages** (runs 2 and 3). Kind of Blue shows "Due Tue 27 Oct 2026" on the kiosk but "26 Oct 2026" on Reception → Active Loans (see K-08 and K-16d). The kiosk converts dates to local time; the staff pages show the stored UTC date. Reported to the team (affects existing staff pages, not only the kiosk).
2. **Camera scanning untested** – needs a person to allow the webcam and hold up a QR card (K-31).
3. **Very narrow screens** (run 3, K-29b). At a 390 px layout width there is no sideways scrolling, but the welcome page content needs 381 px, so a layout width below 381 px (for example a 360 px phone, or a 390 px window that also shows a desktop scrollbar) scrolls sideways by a few pixels. Possible fix: let `.kiosk-start-intro` and `.kiosk-start-card` shrink (`min-width: 0` / smaller side padding below 400 px).
4. **Location name** – when the site is started from Visual Studio, the kiosk shows the default "Bibliotheca Library". Set `Kiosk:BranchName` in `appsettings.json` or use the setup script's `-Location` option for each library.

Results below are from run 3.

## Seeded test data

| Card | Patron | State at start | Use it to test |
| :--- | :--- | :--- | :--- |
| `CRD-1001` | David Smith | *The Hobbit* overdue (≈4 days) | Overdue warning, fine so far, borrowing blocked |
| `CRD-1002` | Emma Watson | *Kind of Blue* on loan, not overdue | Normal account summary and checkout |
| `CRD-1003` | Michael Brown | No current loans, one paid fine | Empty loans list |
| `CRD-1004` | Sophia Davis | No history | New patron |

| Item code | Item | Status |
| :--- | :--- | :--- |
| `BOK-1001`, `BOK-1003`, `BOK-1005`, `MUS-2001`, `MUS-2003`, `TOY-3001`, `TOY-3002`, `TOY-3004` | Various | Available |
| `BOK-1002` (The Hobbit), `MUS-2002` (Kind of Blue) | | Borrowed |
| `BOK-1004`, `TOY-3003` | | Damaged |
| `MUS-2004` | Rumours | Destroyed (hidden from kiosk search) |

## Test cases

| ID | Area | Steps | Expected result | Actual result (run 3) | Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- |
| K-01 | Route | Open `/Kiosk` from a clean run | Kiosk welcome screen with keypad, location name in header, no staff menus | Pass – welcome screen with keypad, no staff menus. Header shows the default name "Bibliotheca Library" (Visual Studio profile, no location set). | K-01-kiosk-welcome.jpg |
| K-02 | Lookup – empty | Tap **Continue** with an empty box | "Please scan or type your library card number." | Pass – "Please scan or type your library card number." | K-02-empty-card.jpg |
| K-03 | Lookup – invalid | Enter `CRD-9999` | "We couldn't find that library card…" and stays on welcome screen | Pass – "We couldn't find that library card. Check the number and try again, or ask at the front desk." Stayed on the welcome screen. | K-03-invalid-card.jpg |
| K-04 | Lookup – valid | Use keypad: **CRD-**, 1, 0, 0, 2, **Continue** | "Hello, Emma" account page | Pass – on-screen keys entered `CRD-1002`; Continue opened "Hello, Emma" | K-04-keypad-emma.jpg |
| K-05 | Lookup – case | Enter `crd-1002` (lower case) | Same as K-04 | Pass – `crd-1002` opened Emma's account | K-05-lowercase-card.jpg |
| K-06 | Lookup – inactive | Reception edits a borrower → set *Account Status* to inactive; enter that card | "This library card is not active…" | Pass – Reception set Sophia (CRD-1004) to Suspended / Inactive → kiosk: "This library card is not active. Please see the front desk for help." Sophia set back to Active afterwards. | K-06a-set-inactive.jpg, K-06b-inactive-card.jpg |
| K-07 | Lockout | Enter 5 wrong card numbers in a row | "Too many unsuccessful attempts", box disabled, countdown, unlocks after 60 s | Pass – after 5 wrong cards: "Too many unsuccessful attempts…", box and buttons disabled, "You can try again in 49 seconds"; about 60 s later the box was enabled again | K-07a-lockout.jpg, K-07b-unlocked.jpg |
| K-08 | Account summary | Log in with `CRD-1002` | 1 item on loan with due date; unpaid fines $0.00; reservations note; card shown as `•••• 1002`; no email/phone/address | Pass – "Hello, Emma", card •••• 1002, Kind of Blue due Tue 27 Oct 2026 (11 days left), $0.00 fines, reservations note; no email/phone/address | K-08-account-summary.jpg |
| K-09 | Overdue | Log in with `CRD-1001` | *The Hobbit* marked overdue with days and "fine so far"; "Self-service borrowing is paused" banner | Pass – The Hobbit "Overdue 5 days", fine so far $5.00, banner "Self-service borrowing is paused" | K-09-overdue-david.jpg |
| K-10 | Unpaid fines shown | Reception returns *The Hobbit* for CRD-1001 **without** paying the fine; then log in with `CRD-1001` at kiosk | Fine listed under *Unpaid fines* with the amount | Pass – returned at the desk with "Collect now" unticked; kiosk shows Unpaid fines $5.00 (The Hobbit, 5 days late), nothing on loan | K-10-unpaid-fine-shown.jpg |
| K-11 | Search – title | Search `hobbit` | *The Hobbit* shown as "On loan", no Borrow button | Pass – The Hobbit "On loan", no Borrow button | K-11-search-hobbit.jpg |
| K-12 | Search – author | Search `orwell` | *1984* shown | Pass – 1984 (BOK-1005) found | K-12-search-orwell.jpg |
| K-13 | Search – category | Tap **Toys & games** | Only toys listed; available ones have **Borrow** | Pass – 4 toys only; the 3 available have Borrow, the damaged puzzle shows Unavailable | K-13-search-toys.jpg |
| K-14 | Search – no results | Search `zzzz` | "No items matched your search…" | Pass – "No items matched your search. Try a different word or category." | K-14-search-no-results.jpg |
| K-15 | Search – anonymous | Without a card, tap **Just browse the catalogue** | Search works; button says "Scan card to borrow"; no Borrow buttons | Pass – no card: `orwell` found 1984 as Available with no Borrow button; top button "Scan card to borrow" | K-15-anonymous-browse.jpg |
| K-16 | Checkout – available | As CRD-1002, Search → Borrow `TOY-3002` → **Confirm and borrow** | Receipt with item, due date 14 days ahead, "2 items on loan"; item now "On loan" in search and in Reception *Active Loans* | Pass – confirm page "Due back on Friday 30 October 2026 (14 days)" → receipt "You now have 2 items on loan"; Catan now "On loan" in search and listed for Emma in Reception Active Loans | K-16a-confirm.jpg, K-16b-receipt.jpg, K-16c-now-on-loan.jpg, K-16d-reception-active-loans.jpg |
| K-17 | Checkout – by code | Borrow an item → type `BOK-1005` → **Find item** → **Confirm** | Receipt shown | Pass – receipt for 1984, "You now have 3 items on loan" | K-17-checkout-by-code.jpg |
| K-18 | Checkout – borrowed item | Borrow an item → `BOK-1002` | "…is already on loan to someone else…", no Confirm button | Pass – "'The Hobbit' is already on loan to someone else. Ask staff about placing a hold." No Confirm button | K-18-borrowed-item.jpg |
| K-19 | Checkout – damaged item | Borrow an item → `BOK-1004` | "…is being repaired…", no Confirm button | Pass – "'To Kill a Mockingbird' is being repaired and can't be borrowed right now…" No Confirm button | K-19-damaged-item.jpg |
| K-20 | Checkout – unknown code | Borrow an item → `XYZ-0000` | "We couldn't find an item with code 'XYZ-0000'…" | Pass – "We couldn't find an item with code 'XYZ-0000'. Check the label and try again." | K-20-unknown-code.jpg |
| K-21 | Checkout – blocked patron | As CRD-1001, Borrow an item → `TOY-3001` | Overdue message, no Confirm button | Pass – "You have an overdue item. Please return it before borrowing more." No Confirm button | K-21-blocked-patron.jpg |
| K-22 | Checkout – limit | As CRD-1004, borrow 5 items, then try a 6th | "You have reached the self-service limit of 5 items" | Pass – Sophia borrowed BOK-1001, BOK-1003, MUS-2001, MUS-2003, TOY-3001 ("5 items on loan"); 6th (BOK-1002) → "You have reached the self-service limit of 5 items on loan." | K-22a-fifth-item.jpg, K-22b-limit-reached.jpg |
| K-23 | Double tap | Tap **Confirm and borrow** twice quickly | Only one loan is created | Pass – Confirm for TOY-3004 clicked three times in one burst; Emma's account shows 4 items with the robot kit listed once (also once in Reception Active Loans, K-32b) | K-23-double-tap-one-loan.jpg |
| K-24 | End session | Tap **End session** | "You're signed out" on welcome screen | Pass – "You're signed out. Thank you for visiting!" | K-24-end-session.jpg |
| K-25 | Back button | After K-24 press the browser **Back** button | Previous patron's account is **not** shown (redirects to welcome) | Pass – Back showed the welcome screen with "Your kiosk session has ended…", not David's account | K-25-back-button.jpg |
| K-26 | Idle timeout | Log in and leave the kiosk untouched for 90 s | "Are you still there?" overlay with countdown; after 15 s returns to welcome with "…kiosk was idle…" | Pass – normal settings: "Are you still there?" with countdown ("7 seconds" left when captured); with no response the kiosk returned to welcome: "Your session ended because the kiosk was idle." | K-26a-idle-warning.jpg, K-26b-idle-signed-out.jpg |
| K-27 | Idle – stay | When the overlay appears tap **I'm still here** | Overlay closes, session continues | Pass – overlay appeared about 90 s after sign-in; "I'm still here" clicked 2 s later; overlay closed and Michael stayed signed in | K-27-still-here-continues.jpg |
| K-28 | Server-side expiry | Log in, close the tab **without** ending the session, wait 2 minutes, open `/Kiosk/Account` | Welcome screen with "Your kiosk session has ended…", not the previous account | Pass – closed the kiosk tab while Michael was signed in, waited just over 2 minutes, opened /Kiosk/Account in a new tab → "Your kiosk session has ended…" | K-28-server-expiry.jpg |
| K-29 | Touch layout | Open kiosk on a tablet or with browser width ≈ 390 px | Buttons ≥ 56 px tall, no sideways scrolling | Pass – welcome and search pages loaded at a 390 px layout width: page width 390 px (no sideways scrolling), smallest buttons 60 px and 64 px. See finding 3 for widths under 381 px. | K-29a-phone-390px.jpg, K-29b-379px-overflow.jpg |
| K-30 | Keyboard / focus | Use **Tab** through the welcome screen | Every control shows a thick orange focus outline | Pass – real Tab key presses moved focus through the keypad to Continue; focused control shows a 4 px solid orange (#F59E0B) outline | K-30a-focus-keypad.jpg, K-30b-focus-continue.jpg |
| K-31 | Camera scan | Reception → *Kiosk Test Cards (QR)*; at kiosk tap **Scan with camera** and show a card QR | Card number filled in and account opens | Partial – the QR test cards page works for Reception. The camera step was not run: it needs a person to allow the webcam in Chrome and hold a QR card up to it. Retest by hand on a machine with a webcam. | K-31-test-cards-qr.jpg |
| K-32 | Desk unaffected | Reception → *Issue / Borrow Item* with a wrong card, then a valid one | Same messages as before; loan created | Pass – `CRD-9999` → "No borrower found with card number 'CRD-9999'."; then CRD-1003 + BOK-1002 → "Successfully issued 'The Hobbit' (BOK-1002) to Michael Brown (Due: 30 Oct 2026)." | K-32a-desk-wrong-card.jpg, K-32b-desk-valid-loan.jpg |
| K-33 | Staff page protected | Open `/Kiosk/TestCards` while signed out | Redirected to the staff login | Pass – signed out, opened /Kiosk/TestCards → Staff Login with ReturnUrl=/Kiosk/TestCards | K-33-testcards-protected.jpg |

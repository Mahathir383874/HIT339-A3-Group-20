# Self-Service Kiosk — Test Plan and Evidence

Owner: Achintha Fernando (S390909) · Module: Public Self-Service Kiosk (`/Kiosk`)

Start from a clean database (`setup.cmd -ResetDatabase` or delete `bibliotheca.db*`) so the seeded data below is in place.
Record the actual result and a screenshot file name for each test. Record **actual** hours in your task log as you go.

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

| ID | Area | Steps | Expected result | Actual result | Evidence |
| :--- | :--- | :--- | :--- | :--- | :--- |
| K-01 | Route | Open `/Kiosk` from a clean run | Kiosk welcome screen with keypad, location name in header, no staff menus | | |
| K-02 | Lookup – empty | Tap **Continue** with an empty box | "Please scan or type your library card number." | | |
| K-03 | Lookup – invalid | Enter `CRD-9999` | "We couldn't find that library card…" and stays on welcome screen | | |
| K-04 | Lookup – valid | Use keypad: **CRD-**, 1, 0, 0, 2, **Continue** | "Hello, Emma" account page | | |
| K-05 | Lookup – case | Enter `crd-1002` (lower case) | Same as K-04 | | |
| K-06 | Lookup – inactive | Reception edits a borrower → set *Account Status* to inactive; enter that card | "This library card is not active…" | | |
| K-07 | Lockout | Enter 5 wrong card numbers in a row | "Too many unsuccessful attempts", box disabled, countdown, unlocks after 60 s | | |
| K-08 | Account summary | Log in with `CRD-1002` | 1 item on loan with due date; unpaid fines $0.00; reservations note; card shown as `•••• 1002`; no email/phone/address | | |
| K-09 | Overdue | Log in with `CRD-1001` | *The Hobbit* marked overdue with days and "fine so far"; "Self-service borrowing is paused" banner | | |
| K-10 | Unpaid fines shown | Reception returns *The Hobbit* for CRD-1001 **without** paying the fine; then log in with `CRD-1001` at kiosk | Fine listed under *Unpaid fines* with the amount | | |
| K-11 | Search – title | Search `hobbit` | *The Hobbit* shown as "On loan", no Borrow button | | |
| K-12 | Search – author | Search `orwell` | *1984* shown | | |
| K-13 | Search – category | Tap **Toys & games** | Only toys listed; available ones have **Borrow** | | |
| K-14 | Search – no results | Search `zzzz` | "No items matched your search…" | | |
| K-15 | Search – anonymous | Without a card, tap **Just browse the catalogue** | Search works; button says "Scan card to borrow"; no Borrow buttons | | |
| K-16 | Checkout – available | As CRD-1002, Search → Borrow `TOY-3002` → **Confirm and borrow** | Receipt with item, due date 14 days ahead, "2 items on loan"; item now "On loan" in search and in Reception *Active Loans* | | |
| K-17 | Checkout – by code | Borrow an item → type `BOK-1005` → **Find item** → **Confirm** | Receipt shown | | |
| K-18 | Checkout – borrowed item | Borrow an item → `BOK-1002` | "…is already on loan to someone else…", no Confirm button | | |
| K-19 | Checkout – damaged item | Borrow an item → `BOK-1004` | "…is being repaired…", no Confirm button | | |
| K-20 | Checkout – unknown code | Borrow an item → `XYZ-0000` | "We couldn't find an item with code 'XYZ-0000'…" | | |
| K-21 | Checkout – blocked patron | As CRD-1001, Borrow an item → `TOY-3001` | Overdue message, no Confirm button | | |
| K-22 | Checkout – limit | As CRD-1004, borrow 5 items, then try a 6th | "You have reached the self-service limit of 5 items" | | |
| K-23 | Double tap | Tap **Confirm and borrow** twice quickly | Only one loan is created | | |
| K-24 | End session | Tap **End session** | "You're signed out" on welcome screen | | |
| K-25 | Back button | After K-24 press the browser **Back** button | Previous patron's account is **not** shown (redirects to welcome) | | |
| K-26 | Idle timeout | Log in and leave the kiosk untouched for 90 s | "Are you still there?" overlay with countdown; after 15 s returns to welcome with "…kiosk was idle…" | | |
| K-27 | Idle – stay | When the overlay appears tap **I'm still here** | Overlay closes, session continues | | |
| K-28 | Server-side expiry | Log in, close the tab **without** ending the session, wait 2 minutes, open `/Kiosk/Account` | Welcome screen with "Your kiosk session has ended…", not the previous account | | |
| K-29 | Touch layout | Open kiosk on a tablet or with browser width ≈ 390 px | Buttons ≥ 56 px tall, no sideways scrolling | | |
| K-30 | Keyboard / focus | Use **Tab** through the welcome screen | Every control shows a thick orange focus outline | | |
| K-31 | Camera scan | Reception → *Kiosk Test Cards (QR)*; at kiosk tap **Scan with camera** and show a card QR | Card number filled in and account opens | | |
| K-32 | Desk unaffected | Reception → *Issue / Borrow Item* with a wrong card, then a valid one | Same messages as before; loan created | | |
| K-33 | Staff page protected | Open `/Kiosk/TestCards` while signed out | Redirected to the staff login | | |

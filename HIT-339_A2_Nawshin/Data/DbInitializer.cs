using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.Enums;
using Bibliotheca.Services;

namespace Bibliotheca.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // 1. Seed Roles
            string[] roles = { "Admin", "Reception", "Manager" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. Seed Default Staff Accounts
            await SeedUserAsync(userManager, "admin@bibliotheca.com", "Admin@123", "Alice Walker (Admin)", "Admin");
            await SeedUserAsync(userManager, "reception@bibliotheca.com", "Reception@123", "Bob Taylor (Reception)", "Reception");
            await SeedUserAsync(userManager, "manager@bibliotheca.com", "Manager@123", "Maria Garcia (Manager)", "Manager");

            // 3. Seed Items (Books, Music, Toys) if empty
            if (!context.Items.Any())
            {
                var books = new List<Book>
                {
                    new Book
                    {
                        Name = "Clean Code: A Handbook of Agile Software Craftsmanship",
                        Description = "A handbook of agile software craftsmanship and best programming practices.",
                        LibraryCode = "BOK-1001",
                        Status = ItemStatus.Available,
                        Author = "Robert C. Martin",
                        Genre = "Technology",
                        ISBN = "978-0132350884",
                        PageCount = 464,
                        ReplacementCost = 45.00m,
                        ImageUrl = "https://images.unsplash.com/photo-1532012164546-f432f2e3edd4?w=400&q=80"
                    },
                    new Book
                    {
                        Name = "The Hobbit",
                        Description = "A fantasy novel and children's book by English author J. R. R. Tolkien.",
                        LibraryCode = "BOK-1002",
                        Status = ItemStatus.Borrowed,
                        Author = "J.R.R. Tolkien",
                        Genre = "Fantasy",
                        ISBN = "978-0547928227",
                        PageCount = 310,
                        ReplacementCost = 22.50m,
                        ImageUrl = "https://images.unsplash.com/photo-1621351183012-e2f9972dd9bf?w=400&q=80"
                    },
                    new Book
                    {
                        Name = "Design Patterns: Elements of Reusable Object-Oriented Software",
                        Description = "Captures a wealth of experience about the design of object-oriented software.",
                        LibraryCode = "BOK-1003",
                        Status = ItemStatus.Available,
                        Author = "Erich Gamma, Richard Helm, Ralph Johnson, John Vlissides",
                        Genre = "Technology",
                        ISBN = "978-0201633610",
                        PageCount = 395,
                        ReplacementCost = 55.00m,
                        ImageUrl = "https://images.unsplash.com/photo-1544716278-ca5e3f4abd8c?w=400&q=80"
                    },
                    new Book
                    {
                        Name = "To Kill a Mockingbird",
                        Description = "The unforgettable novel of a childhood in a sleepy Southern town and the crisis of conscience that rocked it.",
                        LibraryCode = "BOK-1004",
                        Status = ItemStatus.Damaged,
                        Author = "Harper Lee",
                        Genre = "Classic Literature",
                        ISBN = "978-0061120084",
                        PageCount = 281,
                        ReplacementCost = 18.00m,
                        ImageUrl = "https://images.unsplash.com/photo-1512820790803-83ca734da794?w=400&q=80"
                    },
                    new Book
                    {
                        Name = "1984",
                        Description = "A dystopian social science fiction novel and cautionary tale.",
                        LibraryCode = "BOK-1005",
                        Status = ItemStatus.Available,
                        Author = "George Orwell",
                        Genre = "Dystopian Fiction",
                        ISBN = "978-0451524935",
                        PageCount = 328,
                        ReplacementCost = 19.99m,
                        ImageUrl = "https://images.unsplash.com/photo-1497633762265-9d179a990aa6?w=400&q=80"
                    }
                };

                var musics = new List<Music>
                {
                    new Music
                    {
                        Name = "Abbey Road",
                        Description = "The eleventh studio album by the English rock band the Beatles.",
                        LibraryCode = "MUS-2001",
                        Status = ItemStatus.Available,
                        Artist = "The Beatles",
                        ReleaseYear = 1969,
                        RecordLabel = "Apple Records",
                        DurationMinutes = 47,
                        ReplacementCost = 30.00m,
                        ImageUrl = "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?w=400&q=80"
                    },
                    new Music
                    {
                        Name = "Kind of Blue",
                        Description = "A studio album by American jazz trumpeter, composer, and bandleader Miles Davis.",
                        LibraryCode = "MUS-2002",
                        Status = ItemStatus.Borrowed,
                        Artist = "Miles Davis",
                        ReleaseYear = 1959,
                        RecordLabel = "Columbia",
                        DurationMinutes = 45,
                        ReplacementCost = 28.00m,
                        ImageUrl = "https://images.unsplash.com/photo-1465847899084-d164df4dedc6?w=400&q=80"
                    },
                    new Music
                    {
                        Name = "Thriller",
                        Description = "The sixth studio album by American singer Michael Jackson.",
                        LibraryCode = "MUS-2003",
                        Status = ItemStatus.Available,
                        Artist = "Michael Jackson",
                        ReleaseYear = 1982,
                        RecordLabel = "Epic",
                        DurationMinutes = 42,
                        ReplacementCost = 35.00m,
                        ImageUrl = "https://images.unsplash.com/photo-1511671782779-c97d3d27a1d4?w=400&q=80"
                    },
                    new Music
                    {
                        Name = "Rumours",
                        Description = "The eleventh studio album by British-American rock band Fleetwood Mac.",
                        LibraryCode = "MUS-2004",
                        Status = ItemStatus.Destroyed,
                        Artist = "Fleetwood Mac",
                        ReleaseYear = 1977,
                        RecordLabel = "Warner Bros.",
                        DurationMinutes = 40,
                        ReplacementCost = 25.00m,
                        ImageUrl = "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?w=400&q=80"
                    }
                };

                var toys = new List<Toy>
                {
                    new Toy
                    {
                        Name = "Lego Architecture Studio",
                        Description = "Hands-on architectural modeling kit with 1200+ monochromatic bricks.",
                        LibraryCode = "TOY-3001",
                        Status = ItemStatus.Available,
                        ToyType = "STEM & Construction",
                        RecommendedAge = "12+ years",
                        Manufacturer = "LEGO Group",
                        SafetyWarning = "Choking hazard - small parts. Not for children under 3 years.",
                        ReplacementCost = 120.00m,
                        ImageUrl = "https://images.unsplash.com/photo-1585366119957-e9730b6d0f60?w=400&q=80"
                    },
                    new Toy
                    {
                        Name = "Catan Board Game",
                        Description = "Picture yourself in the era of discoveries: your adventurers have arrived on Catan.",
                        LibraryCode = "TOY-3002",
                        Status = ItemStatus.Available,
                        ToyType = "Board Game",
                        RecommendedAge = "10+ years",
                        Manufacturer = "KOSMOS",
                        SafetyWarning = "Contains small playing pieces and dice.",
                        ReplacementCost = 55.00m,
                        ImageUrl = "https://images.unsplash.com/photo-1610890716171-6b1bb98ffd09?w=400&q=80"
                    },
                    new Toy
                    {
                        Name = "Wooden Montessori Sensory Puzzle",
                        Description = "Natural solid wood sensory shape and texture matching puzzle for toddlers.",
                        LibraryCode = "TOY-3003",
                        Status = ItemStatus.Damaged,
                        ToyType = "Educational",
                        RecommendedAge = "18 months - 4 years",
                        Manufacturer = "Montessori Craft",
                        SafetyWarning = "Non-toxic paint, smooth edges.",
                        ReplacementCost = 35.00m,
                        ImageUrl = "https://images.unsplash.com/photo-1596461404969-9ae70f2830c1?w=400&q=80"
                    },
                    new Toy
                    {
                        Name = "Solar Powered Robot Building Kit",
                        Description = "12-in-1 educational solar robot STEM science toy kit.",
                        LibraryCode = "TOY-3004",
                        Status = ItemStatus.Available,
                        ToyType = "STEM & Robotics",
                        RecommendedAge = "8-14 years",
                        Manufacturer = "Discovery Tech",
                        SafetyWarning = "Small parts; requires adult supervision for solar motor assembly.",
                        ReplacementCost = 45.00m,
                        ImageUrl = "https://images.unsplash.com/photo-1485827404703-89b55fcc595e?w=400&q=80"
                    }
                };

                context.Books.AddRange(books);
                context.Musics.AddRange(musics);
                context.Toys.AddRange(toys);
                await context.SaveChangesAsync();
            }

            // 4. Seed Borrowers if empty
            if (!context.Borrowers.Any())
            {
                var borrowers = new List<Borrower>
                {
                    new Borrower
                    {
                        FirstName = "David",
                        LastName = "Smith",
                        Email = "david.smith@example.com",
                        Phone = "0412-345-678",
                        CardNumber = "CRD-1001",
                        Address = "12 Casuarina Drive, Darwin NT 0810",
                        RegisteredDate = DateTime.UtcNow.AddMonths(-3),
                        IsActive = true
                    },
                    new Borrower
                    {
                        FirstName = "Emma",
                        LastName = "Watson",
                        Email = "emma.watson@example.com",
                        Phone = "0423-456-789",
                        CardNumber = "CRD-1002",
                        Address = "45 Mitchell Street, Darwin NT 0800",
                        RegisteredDate = DateTime.UtcNow.AddMonths(-2),
                        IsActive = true
                    },
                    new Borrower
                    {
                        FirstName = "Michael",
                        LastName = "Brown",
                        Email = "michael.brown@example.com",
                        Phone = "0434-567-890",
                        CardNumber = "CRD-1003",
                        Address = "8 University Avenue, Palmerston NT 0830",
                        RegisteredDate = DateTime.UtcNow.AddMonths(-1),
                        IsActive = true
                    },
                    new Borrower
                    {
                        FirstName = "Sophia",
                        LastName = "Davis",
                        Email = "sophia.davis@example.com",
                        Phone = "0445-678-901",
                        CardNumber = "CRD-1004",
                        Address = "22 Nightcliff Road, Nightcliff NT 0810",
                        RegisteredDate = DateTime.UtcNow.AddDays(-15),
                        IsActive = true
                    }
                };

                context.Borrowers.AddRange(borrowers);
                await context.SaveChangesAsync();
            }

            // 5. Seed Loans & Fines for Rich Statistics Demonstration
            if (!context.Loans.Any())
            {
                var hobbit = context.Books.FirstOrDefault(b => b.LibraryCode == "BOK-1002");
                var milesDavis = context.Musics.FirstOrDefault(m => m.LibraryCode == "MUS-2002");
                var cleanCode = context.Books.FirstOrDefault(b => b.LibraryCode == "BOK-1001");
                var catan = context.Toys.FirstOrDefault(t => t.LibraryCode == "TOY-3002");

                var borrower1 = context.Borrowers.FirstOrDefault(b => b.CardNumber == "CRD-1001");
                var borrower2 = context.Borrowers.FirstOrDefault(b => b.CardNumber == "CRD-1002");
                var borrower3 = context.Borrowers.FirstOrDefault(b => b.CardNumber == "CRD-1003");

                if (hobbit != null && borrower1 != null)
                {
                    // Active Overdue Loan: Due 4 days ago -> Reception can return this and see $4.00 fine calculation!
                    var overdueLoan = new Loan
                    {
                        ItemId = hobbit.Id,
                        BorrowerId = borrower1.Id,
                        BorrowDate = DateTime.UtcNow.AddDays(-18),
                        DueDate = DateTime.UtcNow.AddDays(-4),
                        Status = LoanStatus.Overdue,
                        Remarks = "Loan active, currently overdue."
                    };
                    context.Loans.Add(overdueLoan);
                }

                if (milesDavis != null && borrower2 != null)
                {
                    // Active on-time loan
                    var activeLoan = new Loan
                    {
                        ItemId = milesDavis.Id,
                        BorrowerId = borrower2.Id,
                        BorrowDate = DateTime.UtcNow.AddDays(-3),
                        DueDate = DateTime.UtcNow.AddDays(11),
                        Status = LoanStatus.Active,
                        Remarks = "Standard 14-day borrowing."
                    };
                    context.Loans.Add(activeLoan);
                }

                if (cleanCode != null && borrower3 != null)
                {
                    // Past Returned Loan that was returned late with a paid fine ($5.00)
                    var pastLateLoan = new Loan
                    {
                        ItemId = cleanCode.Id,
                        BorrowerId = borrower3.Id,
                        BorrowDate = DateTime.UtcNow.AddDays(-35),
                        DueDate = DateTime.UtcNow.AddDays(-21),
                        ReturnDate = DateTime.UtcNow.AddDays(-16),
                        Status = LoanStatus.Returned,
                        Remarks = "Returned 5 days late; fine paid at desk."
                    };
                    context.Loans.Add(pastLateLoan);
                    await context.SaveChangesAsync();

                    var paidFine = new Fine
                    {
                        LoanId = pastLateLoan.Id,
                        BorrowerId = borrower3.Id,
                        Amount = 5.00m,
                        DaysLate = 5,
                        CalculationDetails = "5 days overdue @ $1.00/day",
                        IsPaid = true,
                        PaidDate = DateTime.UtcNow.AddDays(-16),
                        CreatedDate = DateTime.UtcNow.AddDays(-16)
                    };
                    context.Fines.Add(paidFine);
                }

                if (catan != null && borrower1 != null)
                {
                    // Past Returned On-Time Loan
                    var pastOnTimeLoan = new Loan
                    {
                        ItemId = catan.Id,
                        BorrowerId = borrower1.Id,
                        BorrowDate = DateTime.UtcNow.AddDays(-50),
                        DueDate = DateTime.UtcNow.AddDays(-36),
                        ReturnDate = DateTime.UtcNow.AddDays(-38),
                        Status = LoanStatus.Returned,
                        Remarks = "Returned early in pristine condition."
                    };
                    context.Loans.Add(pastOnTimeLoan);
                }

                await context.SaveChangesAsync();
            }

            // 6. Seed branches, branch-aware inventory, and demo holds / transfers / notifications
            await SeedBranchDataAsync(context, userManager, serviceProvider);
        }

        // ------------------------------------------------------------------
        // Multi-branch, reservations, transfers and notifications (demo data)
        // ------------------------------------------------------------------
        private static async Task SeedBranchDataAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IServiceProvider serviceProvider)
        {
            // Runs once: the branch table is empty only on the first start with this schema.
            if (context.Branches.Any()) return;

            var darwin = new Branch { Name = "Darwin City Library", Code = "DAR", Address = "Bennett Street, Darwin City NT 0800", IsActive = true };
            var palmerston = new Branch { Name = "Palmerston Library", Code = "PAL", Address = "Chung Wah Terrace, Palmerston NT 0830", IsActive = true };
            var casuarina = new Branch { Name = "Casuarina Library", Code = "CAS", Address = "Trower Road, Casuarina NT 0810", IsActive = true };
            context.Branches.AddRange(darwin, palmerston, casuarina);
            await context.SaveChangesAsync();

            // Which branch holds which seeded item (every item is assigned to exactly one branch)
            var placement = new Dictionary<string, Branch>
            {
                ["BOK-1001"] = darwin, ["BOK-1002"] = darwin, ["BOK-1003"] = palmerston, ["BOK-1004"] = darwin, ["BOK-1005"] = casuarina,
                ["MUS-2001"] = darwin, ["MUS-2002"] = palmerston, ["MUS-2003"] = casuarina, ["MUS-2004"] = palmerston,
                ["TOY-3001"] = casuarina, ["TOY-3002"] = darwin, ["TOY-3003"] = palmerston, ["TOY-3004"] = casuarina
            };

            var items = context.Items.ToList();
            foreach (var item in items.Where(i => !i.BranchId.HasValue))
            {
                item.BranchId = placement.TryGetValue(item.LibraryCode, out var branch) ? branch.Id : darwin.Id;
            }
            await context.SaveChangesAsync();

            // Reception and admin desks are at the Darwin City branch
            foreach (var email in new[] { "admin@bibliotheca.com", "reception@bibliotheca.com" })
            {
                var user = await userManager.FindByEmailAsync(email);
                if (user != null && !user.DeskBranchId.HasValue)
                {
                    user.DeskBranchId = darwin.Id;
                    await userManager.UpdateAsync(user);
                }
            }

            var notifications = serviceProvider.GetRequiredService<INotificationService>();
            var now = DateTime.UtcNow;

            Item? Find(string code) => items.FirstOrDefault(i => i.LibraryCode == code);
            Borrower? Patron(string card) => context.Borrowers.FirstOrDefault(b => b.CardNumber == card);

            // ---- Transfer history (no transfer is left in progress, so the demo items stay borrowable) ----
            var nineteen84 = Find("BOK-1005");
            var abbeyRoad = Find("MUS-2001");
            if (nineteen84 != null)
            {
                context.BranchTransfers.Add(new BranchTransfer
                {
                    ItemId = nineteen84.Id,
                    FromBranchId = palmerston.Id,
                    ToBranchId = casuarina.Id,
                    Status = TransferStatus.Completed,
                    RequestedDate = now.AddDays(-12),
                    RequestedBy = "Alice Walker (Admin)",
                    DispatchedDate = now.AddDays(-11),
                    DispatchedBy = "Bob Taylor (Reception)",
                    CompletedDate = now.AddDays(-9),
                    CompletedBy = "Bob Taylor (Reception)",
                    Notes = "Move to Casuarina where demand is higher."
                });
            }

            if (abbeyRoad != null)
            {
                context.BranchTransfers.Add(new BranchTransfer
                {
                    ItemId = abbeyRoad.Id,
                    FromBranchId = darwin.Id,
                    ToBranchId = palmerston.Id,
                    Status = TransferStatus.Cancelled,
                    RequestedDate = now.AddDays(-6),
                    RequestedBy = "Bob Taylor (Reception)",
                    CancelledDate = now.AddDays(-5),
                    CancelledBy = "Alice Walker (Admin)",
                    Notes = "Requested for a display.",
                    CancelReason = "Display cancelled."
                });
            }

            await context.SaveChangesAsync();

            // ---- Holds: a queue on The Hobbit (on loan) and one on the damaged Mockingbird ----
            var hobbit = Find("BOK-1002");
            var mockingbird = Find("BOK-1004");
            var michael = Patron("CRD-1003");
            var sophia = Patron("CRD-1004");

            var placed = new List<(Reservation Reservation, Borrower Borrower, Item Item, int Position)>();
            if (hobbit != null && michael != null)
            {
                var r = new Reservation { ItemId = hobbit.Id, BorrowerId = michael.Id, Status = ReservationStatus.Waiting, CreatedDate = now.AddDays(-3), PlacedVia = "Kiosk" };
                context.Reservations.Add(r);
                await context.SaveChangesAsync();
                placed.Add((r, michael, hobbit, 1));
            }

            if (hobbit != null && sophia != null)
            {
                var r = new Reservation { ItemId = hobbit.Id, BorrowerId = sophia.Id, Status = ReservationStatus.Waiting, CreatedDate = now.AddDays(-2), PlacedVia = "Reception desk" };
                context.Reservations.Add(r);
                await context.SaveChangesAsync();
                placed.Add((r, sophia, hobbit, 2));
            }

            if (mockingbird != null && michael != null)
            {
                var r = new Reservation { ItemId = mockingbird.Id, BorrowerId = michael.Id, Status = ReservationStatus.Waiting, CreatedDate = now.AddDays(-1), PlacedVia = "Kiosk" };
                context.Reservations.Add(r);
                await context.SaveChangesAsync();
                placed.Add((r, michael, mockingbird, 1));
            }

            foreach (var entry in placed)
            {
                var note = notifications.StageReservationPlaced(entry.Borrower, entry.Item, entry.Reservation, entry.Position);
                note.CreatedDate = entry.Reservation.CreatedDate;
            }

            // ---- Notification history for the seeded loans and fine ----
            var loans = context.Loans.ToList();
            var emma = Patron("CRD-1002");
            var kindOfBlue = Find("MUS-2002");
            if (emma != null && kindOfBlue != null)
            {
                var loan = loans.FirstOrDefault(l => l.ItemId == kindOfBlue.Id && l.BorrowerId == emma.Id);
                if (loan != null)
                {
                    var note = notifications.StageBorrowed(emma, kindOfBlue, loan);
                    note.CreatedDate = loan.BorrowDate;
                }
            }

            var paidFine = context.Fines.FirstOrDefault();
            if (paidFine != null)
            {
                var borrower = context.Borrowers.FirstOrDefault(b => b.Id == paidFine.BorrowerId);
                var loan = loans.FirstOrDefault(l => l.Id == paidFine.LoanId);
                var item = loan != null ? items.FirstOrDefault(i => i.Id == loan.ItemId) : null;
                if (borrower != null && loan != null)
                {
                    var issued = notifications.StageFineIssued(borrower, item, loan, paidFine);
                    issued.CreatedDate = paidFine.CreatedDate;
                    var paid = notifications.StageFinePaid(borrower, item, paidFine);
                    paid.CreatedDate = paidFine.PaidDate ?? paidFine.CreatedDate;
                }
            }

            await context.SaveChangesAsync();

            // Due-date reminders for the seeded loans (David's overdue copy of The Hobbit gets an overdue notice)
            await notifications.GenerateDueRemindersAsync();
        }

        // ------------------------------------------------------------------
        // Schema check for databases created by an earlier version of the project
        // ------------------------------------------------------------------
        private static async Task RebuildOutdatedDatabaseAsync(ApplicationDbContext context, IServiceProvider serviceProvider)
        {
            string? dataSource = null;
            try
            {
                var connection = context.Database.GetDbConnection();
                dataSource = connection.DataSource;

                if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:") return;

                var path = Path.GetFullPath(dataSource);
                if (!File.Exists(path) || new FileInfo(path).Length == 0) return; // nothing to upgrade

                var opened = false;
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                    opened = true;
                }

                bool outdated;
                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Branches', 'Reservations', 'BranchTransfers', 'Notifications')";
                    var tables = Convert.ToInt32(await command.ExecuteScalarAsync());

                    using var itemsCheck = connection.CreateCommand();
                    itemsCheck.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Items') WHERE name = 'BranchId'";
                    var itemColumn = Convert.ToInt32(await itemsCheck.ExecuteScalarAsync());

                    using var usersCheck = connection.CreateCommand();
                    usersCheck.CommandText = "SELECT COUNT(*) FROM pragma_table_info('AspNetUsers') WHERE name = 'DeskBranchId'";
                    var userColumn = Convert.ToInt32(await usersCheck.ExecuteScalarAsync());

                    var hasOldTables = await TableExistsAsync(connection, "Items");
                    outdated = hasOldTables && (tables < 4 || itemColumn == 0 || userColumn == 0);
                }
                finally
                {
                    if (opened) await connection.CloseAsync();
                }

                if (!outdated) return;

                await context.Database.CloseConnectionAsync();
                SqliteConnection.ClearAllPools();

                var backup = path + ".old-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                File.Copy(path, backup, overwrite: false);
                await context.Database.EnsureDeletedAsync();
                SqliteConnection.ClearAllPools();

                var logger = serviceProvider.GetService<ILogger<ApplicationDbContext>>();
                logger?.LogWarning("The existing database used an older schema. It was backed up to {Backup} and will be recreated with the current schema and demo data.", backup);
            }
            catch (Exception ex)
            {
                var logger = serviceProvider.GetService<ILogger<ApplicationDbContext>>();
                logger?.LogError(ex, "Could not check the existing database schema ({DataSource}). If pages fail with 'no such table', delete the .db file and restart.", dataSource);
            }
        }

        private static async Task<bool> TableExistsAsync(System.Data.Common.DbConnection connection, string table)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '" + table + "'";
            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }

        private static async Task SeedUserAsync(UserManager<ApplicationUser> userManager, string email, string password, string fullName, string role)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName,
                    StaffRole = role
                };
                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }
        }
    }
}

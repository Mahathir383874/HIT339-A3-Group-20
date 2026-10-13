using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Bibliotheca.Models.Entities;

namespace Bibliotheca.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Item> Items { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<Music> Musics { get; set; }
        public DbSet<Toy> Toys { get; set; }
        public DbSet<Borrower> Borrowers { get; set; }
        public DbSet<Loan> Loans { get; set; }
        public DbSet<Fine> Fines { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<BranchTransfer> BranchTransfers { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Table-Per-Hierarchy (TPH) inheritance mapping for Item superclass
            builder.Entity<Item>()
                .ToTable("Items")
                .HasDiscriminator<string>("Discriminator")
                .HasValue<Book>("Book")
                .HasValue<Music>("Music")
                .HasValue<Toy>("Toy");

            // Unique index for Item Library Code
            builder.Entity<Item>()
                .HasIndex(i => i.LibraryCode)
                .IsUnique();

            // Unique index for Borrower Card Number
            builder.Entity<Borrower>()
                .HasIndex(b => b.CardNumber)
                .IsUnique();

            // Configure Loan relationships
            builder.Entity<Loan>()
                .HasOne(l => l.Item)
                .WithMany(i => i.Loans)
                .HasForeignKey(l => l.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Loan>()
                .HasOne(l => l.Borrower)
                .WithMany(b => b.Loans)
                .HasForeignKey(l => l.BorrowerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Fine relationships
            builder.Entity<Fine>()
                .HasOne(f => f.Loan)
                .WithOne(l => l.Fine)
                .HasForeignKey<Fine>(f => f.LoanId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Fine>()
                .HasOne(f => f.Borrower)
                .WithMany(b => b.Fines)
                .HasForeignKey(f => f.BorrowerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Decimal precision configuration
            builder.Entity<Fine>()
                .Property(f => f.Amount)
                .HasPrecision(18, 2);

            builder.Entity<Item>()
                .Property(i => i.ReplacementCost)
                .HasPrecision(18, 2);

            // ---- Multi-branch inventory ----
            builder.Entity<Branch>()
                .HasIndex(b => b.Code)
                .IsUnique();

            builder.Entity<Branch>()
                .HasIndex(b => b.Name)
                .IsUnique();

            builder.Entity<Item>()
                .HasOne(i => i.Branch)
                .WithMany(b => b.Items)
                .HasForeignKey(i => i.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- Inter-branch transfers (rows are kept as history) ----
            builder.Entity<BranchTransfer>()
                .HasOne(t => t.Item)
                .WithMany(i => i.Transfers)
                .HasForeignKey(t => t.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<BranchTransfer>()
                .HasOne(t => t.FromBranch)
                .WithMany()
                .HasForeignKey(t => t.FromBranchId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BranchTransfer>()
                .HasOne(t => t.ToBranch)
                .WithMany()
                .HasForeignKey(t => t.ToBranchId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- Reservations / waitlist ----
            builder.Entity<Reservation>()
                .HasOne(r => r.Item)
                .WithMany(i => i.Reservations)
                .HasForeignKey(r => r.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Reservation>()
                .HasOne(r => r.Borrower)
                .WithMany(b => b.Reservations)
                .HasForeignKey(r => r.BorrowerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Reservation>()
                .HasOne(r => r.PickupBranch)
                .WithMany()
                .HasForeignKey(r => r.PickupBranchId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Reservation>()
                .HasIndex(r => new { r.ItemId, r.Status });

            // ---- Simulated notifications (kept even if the related record is later removed) ----
            builder.Entity<Notification>()
                .HasOne(n => n.Borrower)
                .WithMany()
                .HasForeignKey(n => n.BorrowerId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Notification>()
                .HasOne(n => n.Item)
                .WithMany()
                .HasForeignKey(n => n.ItemId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Notification>()
                .HasOne(n => n.Loan)
                .WithMany()
                .HasForeignKey(n => n.LoanId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Notification>()
                .HasOne(n => n.Reservation)
                .WithMany()
                .HasForeignKey(n => n.ReservationId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}

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
        }
    }
}

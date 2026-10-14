using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bibliotheca.Models.Entities
{
    /// <summary>
    /// Borrower entity representing public library patrons.
    /// Managed by Reception staff.
    /// </summary>
    public class Borrower
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [NotMapped]
        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
        [Display(Name = "Phone Number")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Library card number is required")]
        [StringLength(30, ErrorMessage = "Card number cannot exceed 30 characters")]
        [Display(Name = "Library Card Number")]
        public string CardNumber { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
        [Display(Name = "Home Address")]
        public string? Address { get; set; }

        [Display(Name = "Date Registered")]
        public DateTime RegisteredDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Membership Active")]
        public bool IsActive { get; set; } = true;

        // Navigation collections
        public virtual ICollection<Loan> Loans { get; set; } = new List<Loan>();
        public virtual ICollection<Fine> Fines { get; set; } = new List<Fine>();
        public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}

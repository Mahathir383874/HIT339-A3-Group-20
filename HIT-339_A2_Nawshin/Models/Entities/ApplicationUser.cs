using Microsoft.AspNetCore.Identity;

namespace Bibliotheca.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? StaffRole { get; set; }

        /// <summary>Branch whose reception desk this staff member works at (null = not assigned to a desk).</summary>
        public int? DeskBranchId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

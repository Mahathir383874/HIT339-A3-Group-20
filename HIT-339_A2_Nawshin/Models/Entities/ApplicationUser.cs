using Microsoft.AspNetCore.Identity;

namespace Bibliotheca.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? StaffRole { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

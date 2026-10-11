using System.ComponentModel.DataAnnotations;

namespace Bibliotheca.Models.Entities
{
    /// <summary>
    /// A physical library branch. Every catalogue item is held at one branch at a time.
    /// </summary>
    public class Branch
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Branch name is required")]
        [StringLength(100, ErrorMessage = "Branch name cannot exceed 100 characters")]
        [Display(Name = "Branch Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Branch code is required")]
        [RegularExpression(@"^[A-Za-z0-9]{2,10}$", ErrorMessage = "Branch code must be 2-10 letters or digits")]
        [Display(Name = "Branch Code")]
        public string Code { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Display(Name = "Open / Active")]
        public bool IsActive { get; set; } = true;

        public virtual ICollection<Item> Items { get; set; } = new List<Item>();
    }
}

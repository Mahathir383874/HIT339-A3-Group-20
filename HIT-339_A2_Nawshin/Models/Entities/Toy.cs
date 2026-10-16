using System.ComponentModel.DataAnnotations;

namespace Bibliotheca.Models.Entities
{
    /// <summary>
    /// Derived Toy entity inheriting from the Item superclass.
    /// </summary>
    public class Toy : Item
    {
        public Toy()
        {
            ItemType = "Toy";
        }

        [Required(ErrorMessage = "Toy type/category is required")]
        [StringLength(60, ErrorMessage = "Toy type cannot exceed 60 characters")]
        [Display(Name = "Toy Type / Category")]
        public string ToyType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Recommended age is required")]
        [StringLength(40, ErrorMessage = "Recommended age cannot exceed 40 characters")]
        [Display(Name = "Recommended Age Group")]
        public string RecommendedAge { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Manufacturer cannot exceed 100 characters")]
        [Display(Name = "Manufacturer / Brand")]
        public string? Manufacturer { get; set; }

        [StringLength(250, ErrorMessage = "Safety warning cannot exceed 250 characters")]
        [Display(Name = "Safety Warning / Notes")]
        public string? SafetyWarning { get; set; }
    }
}

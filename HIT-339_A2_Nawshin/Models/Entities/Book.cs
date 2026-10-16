using System.ComponentModel.DataAnnotations;

namespace Bibliotheca.Models.Entities
{
    /// <summary>
    /// Derived Book entity inheriting from the Item superclass.
    /// </summary>
    public class Book : Item
    {
        public Book()
        {
            ItemType = "Book";
        }

        [Required(ErrorMessage = "Author name is required")]
        [StringLength(100, ErrorMessage = "Author cannot exceed 100 characters")]
        [Display(Name = "Author")]
        public string Author { get; set; } = string.Empty;

        [Required(ErrorMessage = "Genre is required")]
        [StringLength(50, ErrorMessage = "Genre cannot exceed 50 characters")]
        [Display(Name = "Genre")]
        public string Genre { get; set; } = string.Empty;

        [StringLength(20, ErrorMessage = "ISBN cannot exceed 20 characters")]
        [Display(Name = "ISBN")]
        public string? ISBN { get; set; }

        [Range(1, 10000, ErrorMessage = "Page count must be greater than zero")]
        [Display(Name = "Page Count")]
        public int? PageCount { get; set; }
    }
}

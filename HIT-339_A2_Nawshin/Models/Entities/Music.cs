using System.ComponentModel.DataAnnotations;

namespace Bibliotheca.Models.Entities
{
    /// <summary>
    /// Derived Music entity inheriting from the Item superclass.
    /// </summary>
    public class Music : Item
    {
        public Music()
        {
            ItemType = "Music";
        }

        [Required(ErrorMessage = "Artist / Band name is required")]
        [StringLength(100, ErrorMessage = "Artist cannot exceed 100 characters")]
        [Display(Name = "Artist / Band")]
        public string Artist { get; set; } = string.Empty;

        [Range(1900, 2099, ErrorMessage = "Release year must be between 1900 and 2099")]
        [Display(Name = "Release Year")]
        public int? ReleaseYear { get; set; }

        [StringLength(100, ErrorMessage = "Record label cannot exceed 100 characters")]
        [Display(Name = "Record Label")]
        public string? RecordLabel { get; set; }

        [Range(1, 600, ErrorMessage = "Duration must be between 1 and 600 minutes")]
        [Display(Name = "Duration (Minutes)")]
        public int? DurationMinutes { get; set; }
    }
}

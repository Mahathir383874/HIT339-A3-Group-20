using System.Text.Json.Serialization;

namespace Bibliotheca.Models.Api
{
    /// <summary>
    /// Public, read-only view of a catalogue item. Deliberately excludes internal data
    /// such as replacement cost, loan history, borrowers and fines.
    /// Type-specific fields are only written to the JSON when they apply to the item.
    /// </summary>
    public class ItemDto
    {
        public int Id { get; set; }
        public string LibraryCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public string? ImageUrl { get; set; }

        // Book
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Author { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Genre { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Isbn { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? PageCount { get; set; }

        // Music
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Artist { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ReleaseYear { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RecordLabel { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DurationMinutes { get; set; }

        // Toy
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToyType { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RecommendedAge { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Manufacturer { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SafetyWarning { get; set; }
    }

    public class PagedResult<T>
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public List<T> Items { get; set; } = new List<T>();
    }

    public class CategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public int AvailableItems { get; set; }
    }

    public class SystemStatusDto
    {
        public string Service { get; set; } = "Bibliotheca Library API";
        public string ApiVersion { get; set; } = "v1";
        public string Status { get; set; } = "Operational";
        public DateTime ServerTimeUtc { get; set; }
        public int TotalItems { get; set; }
        public int ActiveLoans { get; set; }
        public Dictionary<string, int> ItemsByStatus { get; set; } = new Dictionary<string, int>();
    }
}

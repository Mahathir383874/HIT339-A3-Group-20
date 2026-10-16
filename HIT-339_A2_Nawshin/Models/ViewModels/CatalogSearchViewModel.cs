using Bibliotheca.Models.Entities;

namespace Bibliotheca.Models.ViewModels
{
    public class CatalogSearchViewModel
    {
        public string? SearchTerm { get; set; }
        public string? Category { get; set; } = "All";
        public string? StatusFilter { get; set; } = "All";
        public string? Genre { get; set; }
        public string? ToyType { get; set; }
        public string? SortBy { get; set; } = "name";

        public List<Item> Items { get; set; } = new List<Item>();
        public int TotalResults => Items.Count;
        public int AvailableResults => Items.Count(i => i.Status == Enums.ItemStatus.Available);

        public List<string> AvailableGenres { get; set; } = new List<string>();
        public List<string> AvailableToyTypes { get; set; } = new List<string>();
    }
}

using Bibliotheca.Models.Entities;

namespace Bibliotheca.Services
{
    /// <summary>
    /// Keyword search shared by the public catalogue (CatalogController) and the kiosk,
    /// so both search the same fields: title, description, library code, author, genre,
    /// artist, toy type and manufacturer.
    /// </summary>
    public static class CatalogSearchExtensions
    {
        public static IQueryable<Item> ApplyKeywordSearch(this IQueryable<Item> query, string? searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm)) return query;

            var term = searchTerm.Trim().ToLower();
            return query.Where(i =>
                i.Name.ToLower().Contains(term) ||
                (i.Description != null && i.Description.ToLower().Contains(term)) ||
                i.LibraryCode.ToLower().Contains(term) ||
                i.ItemType.ToLower().Contains(term) ||
                (i is Book && ((Book)i).Author.ToLower().Contains(term)) ||
                (i is Book && ((Book)i).Genre.ToLower().Contains(term)) ||
                (i is Music && ((Music)i).Artist.ToLower().Contains(term)) ||
                (i is Toy && ((Toy)i).ToyType.ToLower().Contains(term)) ||
                (i is Toy && ((Toy)i).Manufacturer != null && ((Toy)i).Manufacturer!.ToLower().Contains(term)));
        }

        public static IQueryable<Item> ApplyCategory(this IQueryable<Item> query, string? category)
        {
            if (string.IsNullOrEmpty(category) || category == "All") return query;
            return query.Where(i => i.ItemType == category);
        }

        /// <summary>Author / artist / maker line for display in compact lists.</summary>
        public static string CreatorLine(this Item item) => item switch
        {
            Book b => b.Author,
            Music m => m.Artist,
            Toy t => string.IsNullOrWhiteSpace(t.Manufacturer) ? t.ToyType : $"{t.Manufacturer} · {t.ToyType}",
            _ => string.Empty
        };

        public static string CategoryLine(this Item item) => item switch
        {
            Book b => $"Book · {b.Genre}",
            Music m => m.ReleaseYear.HasValue ? $"Music · {m.ReleaseYear}" : "Music",
            Toy t => $"Toy · {t.RecommendedAge}",
            _ => item.ItemType
        };
    }
}

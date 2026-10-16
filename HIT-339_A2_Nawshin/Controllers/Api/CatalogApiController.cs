using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bibliotheca.Data;
using Bibliotheca.Models.Api;
using Bibliotheca.Models.Entities;
using Bibliotheca.Services;
using Bibliotheca.Models.Enums;

namespace Bibliotheca.Controllers.Api
{
    /// <summary>
    /// Public, READ-ONLY REST API for external applications.
    /// Only GET endpoints exist: any other HTTP verb on these routes is answered with 405.
    /// No borrower, loan or fine data is exposed.
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [EnableCors("PublicReadOnlyApi")]
    [Route("api/v1")]
    [Produces("application/json")]
    public class CatalogApiController : ControllerBase
    {
        private const int MaxPageSize = 100;
        private const int MaxPage = 1_000_000;
        private const int MaxSearchLength = 100;

        private static readonly string[] Categories = { "Book", "Music", "Toy" };
        private static readonly Regex LibraryCodePattern = new Regex(@"^[A-Z]{3}-\d{4}$", RegexOptions.Compiled);

        private readonly ApplicationDbContext _context;

        public CatalogApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>List catalogue items (paged) with optional filters.</summary>
        /// <param name="category">Book, Music or Toy.</param>
        /// <param name="status">Available, Borrowed, Damaged or Destroyed.</param>
        /// <param name="search">Free text matched against title, code, description, author and artist.</param>
        /// <param name="page">Page number, starting at 1.</param>
        /// <param name="pageSize">Items per page, 1 to 100 (default 20).</param>
        [HttpGet("items")]
        [ProducesResponseType(typeof(PagedResult<ItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetItems(
            string? category,
            string? status,
            string? search,
            int page = 1,
            int pageSize = 20)
        {
            if (page < 1 || page > MaxPage)
            {
                return BadRequestProblem("Invalid page", $"page must be a whole number between 1 and {MaxPage}.");
            }

            if (pageSize < 1 || pageSize > MaxPageSize)
            {
                return BadRequestProblem("Invalid pageSize", $"pageSize must be between 1 and {MaxPageSize}.");
            }

            string? categoryFilter = null;
            if (!string.IsNullOrWhiteSpace(category))
            {
                var requested = category.Trim();
                categoryFilter = Categories.FirstOrDefault(c => c.Equals(requested, StringComparison.OrdinalIgnoreCase));
                if (categoryFilter == null)
                {
                    return BadRequestProblem("Invalid category",
                        $"'{requested}' is not a valid category. Allowed values: {string.Join(", ", Categories)}.");
                }
            }

            ItemStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                var requested = status.Trim();
                if (Enum.TryParse<ItemStatus>(requested, true, out var parsed) && Enum.IsDefined(parsed))
                {
                    statusFilter = parsed;
                }
                else
                {
                    return BadRequestProblem("Invalid status",
                        $"'{requested}' is not a valid status. Allowed values: {string.Join(", ", Enum.GetNames<ItemStatus>())}.");
                }
            }

            if (search != null && search.Length > MaxSearchLength)
            {
                return BadRequestProblem("Invalid search", $"search must be at most {MaxSearchLength} characters.");
            }

            IQueryable<Item> query = _context.Items.AsNoTracking();

            if (categoryFilter != null)
            {
                query = query.Where(i => i.ItemType == categoryFilter);
            }

            if (statusFilter.HasValue)
            {
                var wanted = statusFilter.Value;
                query = query.Where(i => i.Status == wanted);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                // Shared keyword search (same fields as the public catalogue and the kiosk).
                query = query.ApplyKeywordSearch(search);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(i => i.Name)
                .ThenBy(i => i.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new PagedResult<ItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                Items = items.Select(ToDto).ToList()
            };

            return Ok(result);
        }

        /// <summary>Get one catalogue item by its numeric id.</summary>
        [HttpGet("items/{id}")]
        [ProducesResponseType(typeof(ItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetItem(int id)
        {
            if (id <= 0)
            {
                return BadRequestProblem("Invalid id", "id must be a positive whole number.");
            }

            var item = await _context.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
            if (item == null)
            {
                return NotFoundProblem($"No catalogue item exists with id {id}.");
            }

            return Ok(ToDto(item));
        }

        /// <summary>Get one catalogue item by its library code (for example BOK-1001).</summary>
        [HttpGet("items/by-code/{code}")]
        [ProducesResponseType(typeof(ItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetItemByCode(string code)
        {
            var normalised = (code ?? string.Empty).Trim().ToUpperInvariant();
            if (!LibraryCodePattern.IsMatch(normalised))
            {
                return BadRequestProblem("Invalid library code",
                    "Library codes use the format AAA-0000 (for example BOK-1001).");
            }

            var item = await _context.Items.AsNoTracking().FirstOrDefaultAsync(i => i.LibraryCode == normalised);
            if (item == null)
            {
                return NotFoundProblem($"No catalogue item exists with library code {normalised}.");
            }

            return Ok(ToDto(item));
        }

        /// <summary>List the catalogue categories with item counts.</summary>
        [HttpGet("categories")]
        [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCategories()
        {
            var rows = await _context.Items
                .AsNoTracking()
                .Select(i => new { i.ItemType, i.Status })
                .ToListAsync();

            var result = Categories
                .Select(name => new CategoryDto
                {
                    Name = name,
                    TotalItems = rows.Count(r => r.ItemType == name),
                    AvailableItems = rows.Count(r => r.ItemType == name && r.Status == ItemStatus.Available)
                })
                .ToList();

            return Ok(result);
        }

        /// <summary>Operating status of the library service and a summary of stock by status.</summary>
        [HttpGet("status")]
        [ProducesResponseType(typeof(SystemStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SystemStatusDto), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetStatus()
        {
            var dto = new SystemStatusDto { ServerTimeUtc = DateTime.UtcNow };

            foreach (var name in Enum.GetNames<ItemStatus>())
            {
                dto.ItemsByStatus[name] = 0;
            }

            bool reachable;
            try
            {
                reachable = await _context.Database.CanConnectAsync();
            }
            catch (Exception)
            {
                reachable = false;
            }

            if (!reachable)
            {
                dto.Status = "Unavailable";
                return StatusCode(StatusCodes.Status503ServiceUnavailable, dto);
            }

            var statuses = await _context.Items.AsNoTracking().Select(i => i.Status).ToListAsync();
            dto.TotalItems = statuses.Count;
            foreach (var group in statuses.GroupBy(s => s))
            {
                dto.ItemsByStatus[group.Key.ToString()] = group.Count();
            }

            dto.ActiveLoans = await _context.Loans.CountAsync(l => l.Status != LoanStatus.Returned);

            return Ok(dto);
        }

        // ---------- helpers ----------

        private ObjectResult BadRequestProblem(string title, string detail) =>
            Problem(title: title, detail: detail, statusCode: StatusCodes.Status400BadRequest);

        private ObjectResult NotFoundProblem(string detail) =>
            Problem(title: "Item not found", detail: detail, statusCode: StatusCodes.Status404NotFound);

        private static ItemDto ToDto(Item item)
        {
            var dto = new ItemDto
            {
                Id = item.Id,
                LibraryCode = item.LibraryCode,
                Name = item.Name,
                Description = item.Description,
                Category = item.ItemType,
                Status = item.Status.ToString(),
                IsAvailable = item.Status == ItemStatus.Available,
                ImageUrl = item.ImageUrl
            };

            switch (item)
            {
                case Book book:
                    dto.Author = book.Author;
                    dto.Genre = book.Genre;
                    dto.Isbn = book.ISBN;
                    dto.PageCount = book.PageCount;
                    break;
                case Music music:
                    dto.Artist = music.Artist;
                    dto.ReleaseYear = music.ReleaseYear;
                    dto.RecordLabel = music.RecordLabel;
                    dto.DurationMinutes = music.DurationMinutes;
                    break;
                case Toy toy:
                    dto.ToyType = toy.ToyType;
                    dto.RecommendedAge = toy.RecommendedAge;
                    dto.Manufacturer = toy.Manufacturer;
                    dto.SafetyWarning = toy.SafetyWarning;
                    break;
            }

            return dto;
        }
    }
}

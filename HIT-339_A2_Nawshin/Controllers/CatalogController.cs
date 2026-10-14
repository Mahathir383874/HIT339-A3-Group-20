using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bibliotheca.Data;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.Enums;
using Bibliotheca.Models.ViewModels;
using Bibliotheca.Services;

namespace Bibliotheca.Controllers
{
    [AllowAnonymous]
    public class CatalogController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IReservationService _reservations;

        public CatalogController(ApplicationDbContext context, IReservationService reservations)
        {
            _context = context;
            _reservations = reservations;
        }

        // GET: /Catalog
        public async Task<IActionResult> Index(
            string? searchTerm,
            string? category = "All",
            string? statusFilter = "All",
            string? genre = null,
            string? toyType = null,
            string? sortBy = "name",
            int? branchId = null)
        {
            IQueryable<Item> query = _context.Items.Include(i => i.Branch);

            // 0. Filter by branch
            if (branchId.HasValue)
            {
                query = query.Where(i => i.BranchId == branchId.Value);
            }

            // 1. Filter by Category
            query = query.ApplyCategory(category);

            // 2. Filter by Status
            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
            {
                if (Enum.TryParse<ItemStatus>(statusFilter, true, out var parsedStatus))
                {
                    query = query.Where(i => i.Status == parsedStatus);
                }
            }

            // 3. Search Term Keyword Filtering (shared with the Self-Service Kiosk)
            query = query.ApplyKeywordSearch(searchTerm);

            // 4. Specific Genre Filter
            if (!string.IsNullOrWhiteSpace(genre))
            {
                query = query.Where(i => i is Book && ((Book)i).Genre == genre);
            }

            // 5. Specific Toy Type Filter
            if (!string.IsNullOrWhiteSpace(toyType))
            {
                query = query.Where(i => i is Toy && ((Toy)i).ToyType == toyType);
            }

            // 6. Sorting
            query = sortBy switch
            {
                "newest" => query.OrderByDescending(i => i.CreatedDate),
                "code" => query.OrderBy(i => i.LibraryCode),
                "status" => query.OrderBy(i => i.Status),
                _ => query.OrderBy(i => i.Name)
            };

            var items = await query.ToListAsync();

            // Distinct filter lists
            var availableGenres = await _context.Books
                .Select(b => b.Genre)
                .Distinct()
                .OrderBy(g => g)
                .ToListAsync();

            var availableToyTypes = await _context.Toys
                .Select(t => t.ToyType)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            var viewModel = new CatalogSearchViewModel
            {
                SearchTerm = searchTerm,
                Category = category,
                StatusFilter = statusFilter,
                Genre = genre,
                ToyType = toyType,
                SortBy = sortBy,
                BranchId = branchId,
                Branches = await _context.Branches.Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync(),
                Items = items,
                AvailableGenres = availableGenres,
                AvailableToyTypes = availableToyTypes
            };

            return View(viewModel);
        }

        // GET: /Catalog/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var item = await _context.Items
                .Include(i => i.Branch)
                .Include(i => i.Loans.OrderByDescending(l => l.BorrowDate))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            // Hold queue length / transfer state (no patron details are exposed publicly)
            var states = await _reservations.GetLendingStatesAsync(new[] { item.Id });
            ViewBag.Lending = states.TryGetValue(item.Id, out var state) ? state : null;

            return View(item);
        }
    }
}

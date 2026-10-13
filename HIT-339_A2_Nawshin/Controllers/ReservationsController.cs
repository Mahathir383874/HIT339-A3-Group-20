using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bibliotheca.Data;
using Bibliotheca.Models.Enums;
using Bibliotheca.Models.ViewModels;
using Bibliotheca.Services;

namespace Bibliotheca.Controllers
{
    /// <summary>Reception desk: view the waitlist, place a hold for a patron, cancel a hold.</summary>
    [Authorize(Roles = "Reception,Admin")]
    public class ReservationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IReservationService _reservations;

        public ReservationsController(ApplicationDbContext context, IReservationService reservations)
        {
            _context = context;
            _reservations = reservations;
        }

        // GET: /Reservations?filter=Active|History|All&searchTerm=hobbit
        public async Task<IActionResult> Index(string? filter = "Active", string? searchTerm = null)
        {
            var normalised = string.Equals(filter, "History", StringComparison.OrdinalIgnoreCase) ? "History"
                : string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase) ? "All"
                : "Active";

            var model = new ReservationListViewModel
            {
                Filter = normalised,
                SearchTerm = searchTerm,
                Rows = await _reservations.GetRowsAsync(normalised, searchTerm)
            };

            return View(model);
        }

        // GET: /Reservations/Create?libraryCode=BOK-1002
        public async Task<IActionResult> Create(string? libraryCode = null)
        {
            var model = new PlaceHoldViewModel { LibraryCode = libraryCode?.Trim() ?? string.Empty };
            await LoadListsAsync(model);
            return View(model);
        }

        // POST: /Reservations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PlaceHoldViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _reservations.PlaceHoldAsync(model.CardNumber, model.LibraryCode, "Reception desk");
                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }

                var field = result.Error switch
                {
                    ReservationError.CardNotFound or ReservationError.BorrowerInactive => nameof(model.CardNumber),
                    _ => nameof(model.LibraryCode)
                };
                ModelState.AddModelError(field, result.Message);
            }

            await LoadListsAsync(model);
            return View(model);
        }

        // POST: /Reservations/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var result = await _reservations.CancelAsync(id, null, $"Cancelled by staff ({User.Identity?.Name}).");
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadListsAsync(PlaceHoldViewModel model)
        {
            model.HoldableItems = await _context.Items
                .AsNoTracking()
                .Where(i => i.Status != ItemStatus.Destroyed)
                .OrderBy(i => i.LibraryCode)
                .ToListAsync();

            model.ActiveBorrowers = await _context.Borrowers
                .AsNoTracking()
                .Where(b => b.IsActive)
                .OrderBy(b => b.LastName)
                .ThenBy(b => b.FirstName)
                .ToListAsync();
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Bibliotheca.Data;
using Bibliotheca.Models.Enums;
using Bibliotheca.Models.ViewModels;
using Bibliotheca.Services;

namespace Bibliotheca.Controllers
{
    /// <summary>Staff dashboard for the simulated notification log (borrow, due, overdue, fine and hold messages).</summary>
    [Authorize(Roles = "Admin,Reception,Manager")]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notifications;

        public NotificationsController(ApplicationDbContext context, INotificationService notifications)
        {
            _context = context;
            _notifications = notifications;
        }

        // GET: /Notifications?type=ReservationReady&searchTerm=michael
        public async Task<IActionResult> Index(NotificationType? type = null, string? searchTerm = null)
        {
            IQueryable<Bibliotheca.Models.Entities.Notification> query = _context.Notifications
                .AsNoTracking()
                .Include(n => n.Borrower)
                .Include(n => n.Item);

            if (type.HasValue)
            {
                query = query.Where(n => n.Type == type.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(n =>
                    n.Subject.Contains(term) ||
                    n.Message.Contains(term) ||
                    n.Recipient.Contains(term) ||
                    (n.Borrower != null && (n.Borrower.FirstName.Contains(term) || n.Borrower.LastName.Contains(term) || n.Borrower.CardNumber.Contains(term))));
            }

            var total = await query.CountAsync();
            var rows = await query.OrderByDescending(n => n.Id).Take(NotificationListViewModel.MaxRows).ToListAsync();

            var counts = (await _context.Notifications
                    .AsNoTracking()
                    .Select(n => n.Type)
                    .ToListAsync())
                .GroupBy(t => t)
                .ToDictionary(g => g.Key, g => g.Count());

            var model = new NotificationListViewModel
            {
                Type = type,
                SearchTerm = searchTerm,
                Rows = rows,
                TotalCount = total,
                CountsByType = counts
            };

            return View(model);
        }

        // POST: /Notifications/RunReminders
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunReminders()
        {
            var created = await _notifications.GenerateDueRemindersAsync();
            TempData["SuccessMessage"] = created == 0
                ? "Due-date check complete: no new reminders were needed."
                : $"Due-date check complete: {created} new reminder(s) logged.";
            return RedirectToAction(nameof(Index));
        }
    }
}

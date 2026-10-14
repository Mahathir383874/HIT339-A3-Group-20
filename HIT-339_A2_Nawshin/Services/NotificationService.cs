using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Bibliotheca.Data;
using Bibliotheca.Models.Entities;
using Bibliotheca.Models.Enums;

namespace Bibliotheca.Services
{
    /// <summary>Settings bound from the "Notifications" section of appsettings.json.</summary>
    public class NotificationOptions
    {
        public const string SectionName = "Notifications";

        /// <summary>A "due soon" reminder is created when a loan is due within this many days.</summary>
        public int DueSoonDays { get; set; } = 3;

        /// <summary>How often the background worker looks for loans that need a reminder.</summary>
        public int ReminderIntervalMinutes { get; set; } = 30;

        /// <summary>Turn the background reminder worker off (reminders can still be run from the dashboard).</summary>
        public bool EnableBackgroundReminders { get; set; } = true;
    }

    /// <summary>
    /// Simulated notifications. Messages are written to the Notifications table instead of being sent
    /// through a paid e-mail / SMS gateway. The Stage... methods only add rows to the DbContext; the caller
    /// saves, so a notification is stored in the same transaction as the event that caused it.
    /// </summary>
    public interface INotificationService
    {
        Notification StageBorrowed(Borrower borrower, Item item, Loan loan);

        Notification StageFineIssued(Borrower borrower, Item? item, Loan loan, Fine fine);

        Notification StageFinePaid(Borrower borrower, Item? item, Fine fine);

        Notification StageReservationPlaced(Borrower borrower, Item item, Reservation reservation, int queuePosition);

        /// <summary>One e-mail and one SMS record for a hold that has just become ready for pickup.</summary>
        IReadOnlyList<Notification> StageReservationReady(Borrower borrower, Item item, Reservation reservation, string? branchName);

        Notification StageReservationCancelled(Borrower borrower, Item item, Reservation reservation, string reason);

        /// <summary>
        /// Creates "due soon" and "overdue" reminders for open loans that have not been reminded yet
        /// (each loan gets at most one of each kind). Returns the number of reminders created.
        /// </summary>
        Task<int> GenerateDueRemindersAsync(DateTime? asOfUtc = null);

        /// <summary>The patron's most recent messages (e-mail channel only, so SMS copies are not listed twice).</summary>
        Task<List<Notification>> GetForBorrowerAsync(int borrowerId, int take = 10);
    }

    public class NotificationService : INotificationService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // Same rate as CirculationService.DailyFineRate ($1.00 per late day).
        private const decimal DailyFineRate = 1.00m;

        private readonly ApplicationDbContext _context;
        private readonly NotificationOptions _options;

        public NotificationService(ApplicationDbContext context, IOptions<NotificationOptions> options)
        {
            _context = context;
            _options = options.Value;
        }

        public Notification StageBorrowed(Borrower borrower, Item item, Loan loan)
        {
            var subject = $"Loan confirmation: {item.Name}";
            var message = $"Hi {borrower.FirstName}, you borrowed '{item.Name}' ({item.LibraryCode}) on {DateText(loan.BorrowDate)}. " +
                          $"It is due back on {DateText(loan.DueDate)}. Late items are fined ${Money(DailyFineRate)} per day.";
            return Stage(NotificationType.Borrowed, NotificationChannel.Email, borrower, item, loan, null, subject, message);
        }

        public Notification StageFineIssued(Borrower borrower, Item? item, Loan loan, Fine fine)
        {
            var title = item != null ? $"'{item.Name}'" : "a loan";
            var subject = $"Late fine recorded: ${Money(fine.Amount)}";
            var message = $"Hi {borrower.FirstName}, {title} was returned {fine.DaysLate} day(s) late, so a fine of ${Money(fine.Amount)} " +
                          (fine.IsPaid ? "was recorded and paid at the desk. Thank you." : "has been added to your account. You can pay it at the front desk.");
            return Stage(NotificationType.FineIssued, NotificationChannel.Email, borrower, item, loan, null, subject, message);
        }

        public Notification StageFinePaid(Borrower borrower, Item? item, Fine fine)
        {
            var subject = $"Fine payment received: ${Money(fine.Amount)}";
            var message = $"Hi {borrower.FirstName}, we received your payment of ${Money(fine.Amount)}" +
                          (item != null ? $" for '{item.Name}'." : ".") + " Your account has been updated.";
            return Stage(NotificationType.FinePaid, NotificationChannel.Email, borrower, item, null, null, subject, message);
        }

        public Notification StageReservationPlaced(Borrower borrower, Item item, Reservation reservation, int queuePosition)
        {
            var subject = $"Hold placed: {item.Name}";
            var message = $"Hi {borrower.FirstName}, your hold on '{item.Name}' ({item.LibraryCode}) is confirmed. " +
                          $"You are number {queuePosition} in the queue. We will let you know as soon as it is ready for pickup.";
            return Stage(NotificationType.ReservationPlaced, NotificationChannel.Email, borrower, item, null, reservation, subject, message);
        }

        public IReadOnlyList<Notification> StageReservationReady(Borrower borrower, Item item, Reservation reservation, string? branchName)
        {
            var where = string.IsNullOrWhiteSpace(branchName) ? "the front desk" : $"the front desk at {branchName}";
            var subject = $"Ready for pickup: {item.Name}";
            var message = $"Hi {borrower.FirstName}, '{item.Name}' ({item.LibraryCode}) is now ready for you. " +
                          $"Please collect it from {where}, or borrow it at a self-service kiosk with your library card.";

            return new List<Notification>
            {
                Stage(NotificationType.ReservationReady, NotificationChannel.Email, borrower, item, null, reservation, subject, message),
                Stage(NotificationType.ReservationReady, NotificationChannel.Sms, borrower, item, null, reservation, subject, message)
            };
        }

        public Notification StageReservationCancelled(Borrower borrower, Item item, Reservation reservation, string reason)
        {
            var subject = $"Hold cancelled: {item.Name}";
            var message = $"Hi {borrower.FirstName}, your hold on '{item.Name}' ({item.LibraryCode}) was cancelled. {reason}";
            return Stage(NotificationType.ReservationCancelled, NotificationChannel.Email, borrower, item, null, reservation, subject, message);
        }

        public async Task<int> GenerateDueRemindersAsync(DateTime? asOfUtc = null)
        {
            var now = asOfUtc ?? DateTime.UtcNow;
            var windowEnd = now.AddDays(Math.Max(0, _options.DueSoonDays));

            var openLoans = await _context.Loans
                .Include(l => l.Item)
                .Include(l => l.Borrower)
                .Where(l => l.Status != LoanStatus.Returned && l.DueDate <= windowEnd)
                .ToListAsync();

            if (openLoans.Count == 0) return 0;

            var loanIds = openLoans.Select(l => l.Id).ToList();
            var alreadySent = await _context.Notifications
                .Where(n => n.LoanId != null && loanIds.Contains(n.LoanId.Value) &&
                            (n.Type == NotificationType.DueSoon || n.Type == NotificationType.Overdue))
                .Select(n => new { n.LoanId, n.Type })
                .ToListAsync();

            var sent = new HashSet<(int LoanId, NotificationType Type)>(
                alreadySent.Select(n => (n.LoanId!.Value, n.Type)));

            var created = 0;
            foreach (var loan in openLoans)
            {
                if (loan.Borrower == null || loan.Item == null) continue;

                if (loan.DueDate < now)
                {
                    if (sent.Contains((loan.Id, NotificationType.Overdue))) continue;

                    var daysLate = Math.Max(1, (int)Math.Ceiling((now - loan.DueDate).TotalDays));
                    var fineSoFar = daysLate * DailyFineRate;
                    Stage(NotificationType.Overdue, NotificationChannel.Email, loan.Borrower, loan.Item, loan, null,
                        $"Overdue: {loan.Item.Name}",
                        $"Hi {loan.Borrower.FirstName}, '{loan.Item.Name}' ({loan.Item.LibraryCode}) was due on {DateText(loan.DueDate)} and is now {daysLate} day(s) overdue. " +
                        $"The fine so far is ${Money(fineSoFar)} (${Money(DailyFineRate)} per day). Please return it as soon as you can.");
                    created++;
                }
                else
                {
                    if (sent.Contains((loan.Id, NotificationType.DueSoon))) continue;

                    var daysLeft = Math.Max(0, (int)Math.Ceiling((loan.DueDate - now).TotalDays));
                    Stage(NotificationType.DueSoon, NotificationChannel.Email, loan.Borrower, loan.Item, loan, null,
                        $"Reminder: {loan.Item.Name} is due soon",
                        $"Hi {loan.Borrower.FirstName}, '{loan.Item.Name}' ({loan.Item.LibraryCode}) is due back on {DateText(loan.DueDate)} " +
                        $"({(daysLeft <= 1 ? "within a day" : daysLeft + " days from now")}). Returning it on time avoids a ${Money(DailyFineRate)} per day fine.");
                    created++;
                }
            }

            if (created > 0)
            {
                await _context.SaveChangesAsync();
            }

            return created;
        }

        public async Task<List<Notification>> GetForBorrowerAsync(int borrowerId, int take = 10)
        {
            return await _context.Notifications
                .AsNoTracking()
                .Where(n => n.BorrowerId == borrowerId && n.Channel == NotificationChannel.Email)
                .OrderByDescending(n => n.Id)
                .Take(Math.Max(1, take))
                .ToListAsync();
        }

        // ---------------------------------------------------------------
        // helpers
        // ---------------------------------------------------------------

        private Notification Stage(
            NotificationType type,
            NotificationChannel channel,
            Borrower borrower,
            Item? item,
            Loan? loan,
            Reservation? reservation,
            string subject,
            string message)
        {
            var notification = new Notification
            {
                Type = type,
                Channel = channel,
                Recipient = channel == NotificationChannel.Sms ? borrower.Phone : borrower.Email,
                Subject = Truncate(subject, 150),
                Message = Truncate(message, 1000),
                CreatedDate = DateTime.UtcNow
            };

            // Existing rows are linked by id. Rows that are still being created (new loan / new hold)
            // are linked through the navigation property so EF fills in the key when it saves.
            notification.BorrowerId = borrower.Id;
            if (item != null) notification.ItemId = item.Id;

            if (loan != null)
            {
                if (loan.Id != 0) notification.LoanId = loan.Id; else notification.Loan = loan;
            }

            if (reservation != null)
            {
                if (reservation.Id != 0) notification.ReservationId = reservation.Id; else notification.Reservation = reservation;
            }

            _context.Notifications.Add(notification);
            return notification;
        }

        private static string DateText(DateTime value) => value.ToString("d MMM yyyy", Inv);

        private static string Money(decimal value) => value.ToString("F2", Inv);

        private static string Truncate(string value, int max) => value.Length <= max ? value : value.Substring(0, max - 3) + "...";
    }
}

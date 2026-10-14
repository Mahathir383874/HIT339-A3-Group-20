using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bibliotheca.Models.Enums;

namespace Bibliotheca.Models.Entities
{
    /// <summary>
    /// A simulated e-mail / SMS. Nothing is sent to a real gateway; the message is recorded here so it
    /// can be shown on the notification dashboard and on the patron's kiosk account page.
    /// </summary>
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        public NotificationType Type { get; set; }

        public NotificationChannel Channel { get; set; } = NotificationChannel.Email;

        public int? BorrowerId { get; set; }

        [ForeignKey(nameof(BorrowerId))]
        public virtual Borrower? Borrower { get; set; }

        public int? ItemId { get; set; }

        [ForeignKey(nameof(ItemId))]
        public virtual Item? Item { get; set; }

        public int? LoanId { get; set; }

        [ForeignKey(nameof(LoanId))]
        public virtual Loan? Loan { get; set; }

        public int? ReservationId { get; set; }

        [ForeignKey(nameof(ReservationId))]
        public virtual Reservation? Reservation { get; set; }

        /// <summary>E-mail address or phone number the message would have been sent to.</summary>
        [StringLength(150)]
        public string Recipient { get; set; } = string.Empty;

        [StringLength(150)]
        public string Subject { get; set; } = string.Empty;

        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}

namespace Bibliotheca.Models.Enums
{
    public enum NotificationType
    {
        Borrowed,
        DueSoon,
        Overdue,
        FineIssued,
        FinePaid,
        ReservationPlaced,
        ReservationReady,
        ReservationCancelled
    }

    /// <summary>Simulated delivery channel. Nothing is actually sent; the message is only logged.</summary>
    public enum NotificationChannel
    {
        Email,
        Sms
    }
}

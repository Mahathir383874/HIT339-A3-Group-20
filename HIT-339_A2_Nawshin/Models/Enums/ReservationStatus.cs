namespace Bibliotheca.Models.Enums
{
    /// <summary>Lifecycle of a hold: Waiting -> Ready -> Fulfilled, or Cancelled while still active.</summary>
    public enum ReservationStatus
    {
        Waiting,
        Ready,
        Fulfilled,
        Cancelled
    }
}

namespace Bibliotheca.Models.Enums
{
    /// <summary>Lifecycle of an inter-branch transfer: Requested -> InTransit -> Completed (or Cancelled).</summary>
    public enum TransferStatus
    {
        Requested,
        InTransit,
        Completed,
        Cancelled
    }
}

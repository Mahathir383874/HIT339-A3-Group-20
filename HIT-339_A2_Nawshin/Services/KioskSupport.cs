namespace Bibliotheca.Services
{
    /// <summary>
    /// Kiosk settings bound from the "Kiosk" section of appsettings.json.
    /// Every value can be overridden per location on the command line, e.g.
    ///   dotnet run -- --Kiosk:BranchName="Palmerston Library"
    /// or with environment variables, e.g. Kiosk__IdleTimeoutSeconds=120
    /// </summary>
    public class KioskOptions
    {
        public const string SectionName = "Kiosk";

        /// <summary>Name of the physical library location shown in the kiosk header.</summary>
        public string BranchName { get; set; } = "Bibliotheca Library";

        /// <summary>Seconds of inactivity before the kiosk warns and then ends the patron session.</summary>
        public int IdleTimeoutSeconds { get; set; } = 90;

        /// <summary>Seconds the "Are you still there?" warning is shown before the session ends.</summary>
        public int IdleWarningSeconds { get; set; } = 15;

        /// <summary>Loan length used for kiosk checkouts.</summary>
        public int LoanDays { get; set; } = 14;

        /// <summary>Maximum items a patron can hold before the kiosk refuses more checkouts.</summary>
        public int MaxActiveLoans { get; set; } = 5;

        /// <summary>Kiosk refuses checkout while the patron has an overdue item.</summary>
        public bool BlockWhenOverdue { get; set; } = true;

        /// <summary>Kiosk refuses checkout when unpaid recorded fines exceed this amount.</summary>
        public decimal MaxUnpaidFines { get; set; } = 10.00m;

        /// <summary>Failed card attempts allowed before the kiosk pauses lookups.</summary>
        public int MaxFailedCardAttempts { get; set; } = 5;

        /// <summary>How long lookups are paused after too many failed attempts.</summary>
        public int LockoutSeconds { get; set; } = 60;

        /// <summary>Shows the "Scan with camera" button (needs a webcam and http://localhost or HTTPS).</summary>
        public bool EnableCameraScan { get; set; } = true;

        public CheckoutPolicy ToPolicy() => new()
        {
            MaxActiveLoans = MaxActiveLoans,
            BlockWhenOverdue = BlockWhenOverdue,
            MaxUnpaidFines = MaxUnpaidFines
        };
    }

    /// <summary>
    /// Integration point for the Reservation &amp; Waitlist module (Tanisha).
    /// The kiosk account summary asks this service for the patron's holds.
    /// <see cref="ReservationSummaryProvider"/> (in ReservationService.cs) is the registered implementation.
    /// <see cref="NoReservationSummaryProvider"/> is kept as a fallback that makes the kiosk show a
    /// "not available" note; to use it, change the single registration line in Program.cs.
    /// </summary>
    public interface IReservationSummaryProvider
    {
        bool IsEnabled { get; }

        Task<IReadOnlyList<KioskReservationInfo>> GetForBorrowerAsync(int borrowerId);
    }

    public sealed class KioskReservationInfo
    {
        public int Id { get; init; }
        public int ItemId { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public string LibraryCode { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;   // e.g. Waiting, Ready
        public int? QueuePosition { get; init; }
        public string? PickupBranch { get; init; }
    }

    public sealed class NoReservationSummaryProvider : IReservationSummaryProvider
    {
        public bool IsEnabled => false;

        public Task<IReadOnlyList<KioskReservationInfo>> GetForBorrowerAsync(int borrowerId)
            => Task.FromResult<IReadOnlyList<KioskReservationInfo>>(Array.Empty<KioskReservationInfo>());
    }
}

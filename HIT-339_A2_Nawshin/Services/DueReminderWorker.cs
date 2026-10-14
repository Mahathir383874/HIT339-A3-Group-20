using Microsoft.Extensions.Options;

namespace Bibliotheca.Services
{
    /// <summary>
    /// Background worker that periodically writes "due soon" and "overdue" reminders to the notification log.
    /// Staff can also trigger the same check from the Notifications dashboard.
    /// </summary>
    public class DueReminderWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly NotificationOptions _options;
        private readonly ILogger<DueReminderWorker> _logger;

        public DueReminderWorker(IServiceScopeFactory scopeFactory, IOptions<NotificationOptions> options, ILogger<DueReminderWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.EnableBackgroundReminders) return;

            try
            {
                // Give start-up (database creation and seeding) time to finish first.
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                        var created = await notifications.GenerateDueRemindersAsync();
                        if (created > 0)
                        {
                            _logger.LogInformation("Due-date reminders: {Count} notification(s) logged.", created);
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "The due-date reminder check failed; it will be retried at the next interval.");
                    }

                    await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _options.ReminderIntervalMinutes)), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Application is shutting down.
            }
        }
    }
}

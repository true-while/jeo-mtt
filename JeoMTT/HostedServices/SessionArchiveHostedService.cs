using JeoMTT.Services;

namespace JeoMTT.HostedServices
{
    /// <summary>
    /// Background hosted service that archives expired game sessions every 5 minutes.
    /// This service runs continuously and performs periodic cleanup of expired sessions.
    /// </summary>
    public class SessionArchiveHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SessionArchiveHostedService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

        public SessionArchiveHostedService(
            IServiceProvider serviceProvider,
            ILogger<SessionArchiveHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SessionArchiveHostedService is starting (interval: 5 minutes)");

            // Initial delay to allow application to fully start
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            // Run archival in a loop with proper async/await handling
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ArchiveExpiredSessionsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during scheduled archival execution");
                }

                // Wait for the interval before running again
                try
                {
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("SessionArchiveHostedService cancellation requested");
                    break;
                }
            }
        }

        private async Task ArchiveExpiredSessionsAsync()
        {
            try
            {
                _logger.LogInformation("Running session archival at {timestamp} UTC, Local: {localTime}", DateTime.UtcNow, DateTime.Now);

                using (var scope = _serviceProvider.CreateScope())
                {
                    var archiveService = scope.ServiceProvider.GetRequiredService<ISessionArchiveService>();
                    await archiveService.ArchiveExpiredSessionsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while archiving expired sessions");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SessionArchiveHostedService is stopping");
            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}

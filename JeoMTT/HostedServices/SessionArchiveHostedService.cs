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
        }        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SessionArchiveHostedService is starting (interval: 5 minutes)");

            try
            {
                // Initial delay to allow application to fully start
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SessionArchiveHostedService cancellation requested during startup delay");
                return;
            }

            // Run archival in a loop with proper async/await handling
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ArchiveExpiredSessionsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("SessionArchiveHostedService cancellation requested during archival");
                    break;
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
                    _logger.LogInformation("SessionArchiveHostedService cancellation requested during interval delay");
                    break;
                }
            }

            _logger.LogInformation("SessionArchiveHostedService ExecuteAsync completed");
        }        private async Task ArchiveExpiredSessionsAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Running session archival at {timestamp} UTC, Local: {localTime}", DateTime.UtcNow, DateTime.Now);

                using (var scope = _serviceProvider.CreateScope())
                {
                    var archiveService = scope.ServiceProvider.GetRequiredService<ISessionArchiveService>();
                    await archiveService.ArchiveExpiredSessionsAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Session archival was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while archiving expired sessions");
            }
        }        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SessionArchiveHostedService is stopping");
            
            // Give the service a short time to gracefully stop
            // The cancellation token will trigger OperationCanceledException in ExecuteAsync
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10)); // 10 second timeout
            
            try
            {
                await base.StopAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("SessionArchiveHostedService stop timeout exceeded");
            }
            finally
            {
                cts.Dispose();
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}

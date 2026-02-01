using JeoMTT.Data;
using JeoMTT.Models;
using Microsoft.ApplicationInsights;
using Microsoft.EntityFrameworkCore;

namespace JeoMTT.Services
{
    /// <summary>
    /// Service responsible for archiving expired game sessions.
    /// Runs as a background task and archives sessions while preserving score and winner information.
    /// </summary>
    public interface ISessionArchiveService
    {
        Task ArchiveExpiredSessionsAsync();
    }

    public class SessionArchiveService : ISessionArchiveService
    {
        private readonly JeoGameDbContext _context;
        private readonly TelemetryClient _telemetryClient;
        private readonly ILogger<SessionArchiveService> _logger;

        public SessionArchiveService(
            JeoGameDbContext context,
            TelemetryClient telemetryClient,
            ILogger<SessionArchiveService> logger)
        {
            _context = context;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        /// <summary>
        /// Archives all expired active sessions and removes their individual player answers
        /// while preserving the session score and winner information. Uses UTC for all timestamps.
        /// </summary>
        public async Task ArchiveExpiredSessionsAsync()
        {
            try
            {
                var currentTimeUtc = DateTime.UtcNow;
                _logger.LogInformation("Starting session archival process at {timestamp} UTC", currentTimeUtc);                // Find all active sessions that have expired - using UTC comparison
                var expiredSessions = await _context.GameSessions
                    .Where(s => s.Status == GameSessionStatus.Active && s.ExpiresAt < currentTimeUtc)
                    .ToListAsync();

                if (expiredSessions.Count == 0)
                {
                    _logger.LogInformation("No expired sessions found to archive at {timestamp} UTC", currentTimeUtc);
                    return;
                }

                _logger.LogInformation(
                    "Found {count} expired sessions to archive at {timestamp} UTC. Sessions: {sessionDetails}",
                    expiredSessions.Count,
                    currentTimeUtc,
                    string.Join("; ", expiredSessions.Select(s => 
                        $"[{s.Id} - ExpiresAt: {s.ExpiresAt} UTC, SecondsPastExpiry: {(currentTimeUtc - s.ExpiresAt).TotalSeconds}]")));

                int archivedCount = 0;
                int errorCount = 0;

                foreach (var session in expiredSessions)
                {
                    try
                    {
                        // Archive the session                        session.Status = GameSessionStatus.Archived;
                        session.CompletedAt = currentTimeUtc;

                        // Remove game rounds and answers to clean up data
                        // Keep the session score and player information
                        var gameRounds = await _context.GameRounds
                            .Where(gr => gr.GameSessionId == session.Id)
                            .ToListAsync();

                        if (gameRounds != null && gameRounds.Count > 0)
                        {
                            foreach (var round in gameRounds)
                            {
                                _context.RoundAnswers.RemoveRange(round.Answers);
                            }
                            _context.GameRounds.RemoveRange(gameRounds);
                            _logger.LogInformation(
                                "Removed {count} rounds with answers from session {sessionId}",
                                gameRounds.Count,
                                session.Id);
                        }

                        archivedCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogError(
                            ex,
                            "Error archiving session {sessionId}",
                            session.Id);
                        
                        _telemetryClient.TrackException(ex);
                    }
                }

                // Save all changes to the database
                int changesCount = await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Session archival completed: {archived} sessions archived, {errors} errors, {changes} database changes",
                    archivedCount,
                    errorCount,
                    changesCount);

                // Track telemetry
                _telemetryClient.TrackEvent("SessionsArchived", new Dictionary<string, string>
                {
                    { "archivedCount", archivedCount.ToString() },
                    { "errorCount", errorCount.ToString() },
                    { "totalProcessed", expiredSessions.Count.ToString() }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in session archival process");
                _telemetryClient.TrackException(ex);
                throw;
            }
        }
    }
}

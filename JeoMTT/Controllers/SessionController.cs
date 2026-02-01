using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using JeoMTT.Data;
using JeoMTT.Models;
using JeoMTT.Hubs;

namespace JeoMTT.Controllers
{
    public class SessionController : Controller
    {
        private readonly JeoGameDbContext _context;
        private readonly TelemetryClient? _telemetryClient;
        private readonly ILogger<SessionController> _logger;
        private readonly IHubContext<GameSessionHub>? _hubContext;
        private const int SESSION_DURATION_HOURS = 2;

        public SessionController(
            JeoGameDbContext context,
            ILogger<SessionController> logger,
            TelemetryClient? telemetryClient = null,
            IHubContext<GameSessionHub>? hubContext = null)
        {
            _context = context;
            _logger = logger;
            _telemetryClient = telemetryClient;
            _hubContext = hubContext;
        }

        // GET: Session/SessionList
        public IActionResult SessionList(string status = "all")
        {
            return RedirectToAction(nameof(StartNewSession), new { status });
        }

        // GET: Session/StartNewSession
        public async Task<IActionResult> StartNewSession(string status = "all")
        {
            try
            {
                _telemetryClient?.TrackEvent("SessionIndexRequested", new Dictionary<string, string> { { "status", status } });

                // Archive expired sessions
                await ArchiveExpiredSessions();

                IQueryable<GameSession> query = _context.GameSessions
                    .Include(s => s.Game)
                    .Include(s => s.SessionPlayers)
                    .AsQueryable();

                // Filter by status
                if (status != "all")
                {
                    if (Enum.TryParse<GameSessionStatus>(status, true, out var parsedStatus))
                    {
                        query = query.Where(s => s.Status == parsedStatus);
                    }
                }

                var sessions = await query
                    .OrderByDescending(s => s.StartedAt)
                    .ToListAsync();

                ViewBag.CurrentStatus = status;
                return View(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sessions");
                _telemetryClient?.TrackException(ex);
                throw;
            }
        }

        // GET: Session/SessionDetails/5
        public async Task<IActionResult> SessionDetails(Guid? id)
        {
            if (id == null || id == Guid.Empty)
            {
                return NotFound();
            }

            try
            {
                var gameSession = await _context.GameSessions
                    .Include(s => s.Game)
                        .ThenInclude(g => g!.Categories)
                            .ThenInclude(c => c.Questions)
                    .Include(s => s.SessionPlayers)
                    .FirstOrDefaultAsync(s => s.Id == id);

                // Load completed rounds for marking questions as answered
                var completedRounds = await _context.GameRounds
                    .Where(gr => gr.GameSessionId == id && (gr.Status == GameRoundStatus.Ended || gr.Status == GameRoundStatus.Answered))
                    .Select(gr => gr.QuestionId)
                    .ToListAsync();

                // Pass completed question IDs to view
                ViewBag.CompletedQuestionIds = completedRounds;

                if (gameSession == null)
                {
                    return NotFound();
                }

                // Check if session has expired and archive if needed
                if (gameSession.Status == GameSessionStatus.Active && gameSession.ExpiresAt < DateTime.UtcNow)
                {
                    gameSession.Status = GameSessionStatus.Archived;
                    gameSession.CompletedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                _telemetryClient?.TrackEvent("SessionDetailsRetrieved", new Dictionary<string, string>
                {
                    { "sessionId", id.ToString() ?? string.Empty },
                    { "gameId", gameSession.GameId.ToString() }
                });

                // Determine if current user is the admin (session creator)
                var isAdmin = Request.Query.ContainsKey("admin") || 
                             (User.FindFirst("SessionCreator")?.Value == id.ToString());
                ViewBag.IsAdmin = isAdmin;

                return View(gameSession);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving session with id {Id}", id);
                _telemetryClient?.TrackException(ex);
                throw;
            }
        }

        // GET: Session/Create
        [HttpGet]
        public async Task<IActionResult> Create(Guid? gameId)
        {
            try
            {
                _logger.LogInformation("Create session page requested with gameId: {GameId}", gameId?.ToString() ?? "null");

                if (gameId == null || gameId == Guid.Empty)
                {
                    _logger.LogWarning("Invalid gameId provided to Create action: {GameId}", gameId?.ToString() ?? "null");
                    return RedirectToAction("Index", "JeoGame");
                }

                var game = await _context.JeoGames.FindAsync(gameId);
                if (game == null)
                {
                    _logger.LogWarning("Game not found with id: {GameId}", gameId);
                    return RedirectToAction("Index", "JeoGame");
                }

                ViewBag.GameId = gameId;
                ViewBag.GameName = game.Name;

                _telemetryClient?.TrackEvent("CreateSessionPageViewed", new Dictionary<string, string>
                {
                    { "gameId", gameId.ToString() ?? string.Empty },
                    { "gameName", game.Name }
                });

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create session page with gameId {GameId}", gameId?.ToString() ?? "null");
                _telemetryClient?.TrackException(ex);
                return RedirectToAction("Index", "JeoGame");
            }
        }

        // POST: Session/Create
        [HttpPost]
        public async Task<IActionResult> Create([FromForm] Guid gameId)
        {
            try
            {
                _logger.LogInformation("Creating session for gameId: {GameId}", gameId);

                if (gameId == Guid.Empty)
                {
                    _logger.LogWarning("Invalid gameId provided to Create POST action: {GameId}", gameId);
                    _telemetryClient?.TrackEvent("SessionCreationFailed", new Dictionary<string, string>
                    {
                        { "reason", "Empty gameId" }
                    });
                    return BadRequest(new { success = false, message = "Game ID is required to create a session" });
                }

                var game = await _context.JeoGames.FindAsync(gameId);
                if (game == null)
                {
                    _logger.LogWarning("Game not found with id: {GameId}", gameId);
                    _telemetryClient?.TrackEvent("SessionCreationFailed", new Dictionary<string, string>
                    {
                        { "reason", "Game not found" },
                        { "gameId", gameId.ToString() }
                    });
                    return BadRequest(new { success = false, message = $"Game with ID {gameId} not found" });
                }

                var nowUtc = DateTime.UtcNow;
                var expiresAtUtc = nowUtc.AddHours(SESSION_DURATION_HOURS);
                
                var gameSession = new GameSession
                {
                    GameId = gameId,
                    Status = GameSessionStatus.Active,
                    StartedAt = nowUtc,
                    ExpiresAt = expiresAtUtc,
                    QuestionTimerSeconds = 30
                };

                _context.GameSessions.Add(gameSession);
                await _context.SaveChangesAsync();

                gameSession.SessionName = $"{game.Name}-{gameSession.JoinCode}";
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Session created successfully: {SessionId} with join code {JoinCode}",
                    gameSession.Id, gameSession.JoinCode);

                _telemetryClient?.TrackEvent("GameSessionCreated", new Dictionary<string, string>
                {
                    { "sessionId", gameSession.Id.ToString() },
                    { "gameId", gameId.ToString() },
                    { "joinCode", gameSession.JoinCode }
                });

                return Json(new { success = true, gameSessionId = gameSession.Id, joinCode = gameSession.JoinCode });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating game session for game {GameId}", gameId);
                _telemetryClient?.TrackException(ex);
                return StatusCode(500, "Database error: " + ex.InnerException?.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating game session for game {GameId}", gameId);
                _telemetryClient?.TrackException(ex);
                return StatusCode(500, "Error creating game session: " + ex.Message);
            }
        }

        // POST: Session/EndSession/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EndSession(Guid? id)
        {
            if (id == null || id == Guid.Empty)
            {
                return NotFound();
            }

            try
            {
                var gameSession = await _context.GameSessions
                    .Include(s => s.SessionPlayers)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (gameSession == null)
                {
                    return NotFound();
                }

                gameSession.Status = GameSessionStatus.Archived;
                await _context.SaveChangesAsync();

                _telemetryClient?.TrackEvent("GameSessionEnded", new Dictionary<string, string>
                {
                    { "sessionId", id.ToString() ?? string.Empty },
                    { "playerCount", gameSession.SessionPlayers.Count.ToString() }
                });

                return RedirectToAction(nameof(StartNewSession));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending session with id {Id}", id);
                _telemetryClient?.TrackException(ex);
                return StatusCode(500, "Error ending session");
            }
        }

        // NOTE: Old SubmitAnswer endpoint removed - use GameSessionHub.SubmitAnswer() via SignalR instead
        // This supports the new round-based game flow with RoundAnswer model

        // GET: Session/Join
        public IActionResult Join()
        {
            return View();
        }

        // POST: Session/JoinSession
        [HttpPost]
        public async Task<IActionResult> JoinSession([FromBody] JoinSessionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.JoinCode) || string.IsNullOrWhiteSpace(request?.PlayerNickname))
            {
                return BadRequest("Join code and nickname are required");
            }

            if (request.PlayerNickname.Length > 50)
            {
                return BadRequest("Nickname cannot exceed 50 characters");
            }

            try
            {
                var gameSession = await _context.GameSessions
                    .Include(s => s.SessionPlayers)
                    .FirstOrDefaultAsync(s => s.JoinCode == request.JoinCode.ToUpper() && s.Status == GameSessionStatus.Active);

                if (gameSession == null)
                {
                    return BadRequest(new { success = false, message = "Session not found or is no longer active" });
                }

                if (gameSession.ExpiresAt < DateTime.UtcNow)
                {
                    gameSession.Status = GameSessionStatus.Archived;
                    gameSession.CompletedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return BadRequest("Session has expired");
                }

                var existingSessionPlayer = gameSession.SessionPlayers
                    .FirstOrDefault(sp => sp.PlayerNickname.ToLower() == request.PlayerNickname.Trim().ToLower());

                if (existingSessionPlayer != null)
                {
                    return Json(new { success = true, gameSessionId = gameSession.Id, sessionPlayerId = existingSessionPlayer.Id });
                }

                var sessionPlayer = new SessionPlayer
                {
                    GameSessionId = gameSession.Id,
                    PlayerNickname = request.PlayerNickname.Trim(),
                    Score = 0,
                    JoinedAt = DateTime.Now
                };
                _context.SessionPlayers.Add(sessionPlayer);
                await _context.SaveChangesAsync();

                _telemetryClient?.TrackEvent("PlayerJoinedSession", new Dictionary<string, string>
                {
                    { "sessionId", gameSession.Id.ToString() },
                    { "joinCode", request.JoinCode },
                    { "sessionPlayerId", sessionPlayer.Id.ToString() },
                    { "playerNickname", request.PlayerNickname }
                });

                // Broadcast session update via SignalR
                await BroadcastSessionUpdateViaHub(gameSession.Id);

                return Json(new { success = true, gameSessionId = gameSession.Id, sessionPlayerId = sessionPlayer.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining session with code {JoinCode}", request?.JoinCode);
                _telemetryClient?.TrackException(ex);
                return StatusCode(500, "Error joining session");
            }
        }

        private async Task ArchiveExpiredSessions()
        {
            try
            {
                var expiredSessions = await _context.GameSessions
                    .Where(s => s.Status == GameSessionStatus.Active && s.ExpiresAt < DateTime.UtcNow)
                    .ToListAsync();

                foreach (var session in expiredSessions)
                {
                    session.Status = GameSessionStatus.Archived;
                    session.CompletedAt = DateTime.UtcNow;
                }

                if (expiredSessions.Any())
                {
                    await _context.SaveChangesAsync();
                    _telemetryClient?.TrackEvent("SessionsArchived", new Dictionary<string, string>
                    {
                        { "count", expiredSessions.Count.ToString() }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving expired sessions");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSessionData(Guid? id)
        {
            if (id == null || id == Guid.Empty)
            {
                return NotFound();
            }

            try
            {
                var gameSession = await _context.GameSessions
                    .Include(s => s.SessionPlayers)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (gameSession == null)
                {
                    return NotFound();
                }

                var currentPlayerScore = gameSession.SessionPlayers?.FirstOrDefault()?.Score ?? 0;
                var topPlayerScore = gameSession.SessionPlayers?.OrderByDescending(sp => sp.Score).FirstOrDefault()?.Score ?? 0;

                return Ok(new
                {
                    sessionId = gameSession.Id,
                    joinCode = gameSession.JoinCode,
                    playerCount = gameSession.SessionPlayers?.Count ?? 0,
                    status = gameSession.Status.ToString(),
                    timeRemaining = (gameSession.ExpiresAt - DateTime.UtcNow).TotalSeconds,
                    questionTimer = gameSession.QuestionTimerSeconds,
                    currentPlayerScore = currentPlayerScore,
                    topPlayerScore = topPlayerScore
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving session data for id {Id}", id);
                return StatusCode(500, "Error retrieving session data");
            }
        }

        // GET: Session/GameBoard/5
        public async Task<IActionResult> GameBoard(Guid? id)
        {
            if (id == null || id == Guid.Empty)
            {
                return NotFound();
            }

            try
            {
                var gameSession = await _context.GameSessions
                    .Include(s => s.Game)
                        .ThenInclude(g => g!.Categories)
                            .ThenInclude(c => c.Questions)
                    .Include(s => s.SessionPlayers)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (gameSession == null)
                {
                    return NotFound();
                }

                if (gameSession.Status == GameSessionStatus.Active && gameSession.ExpiresAt < DateTime.UtcNow)
                {
                    gameSession.Status = GameSessionStatus.Archived;
                    gameSession.CompletedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                _telemetryClient?.TrackEvent("GameBoardAccessed", new Dictionary<string, string>
                {
                    { "sessionId", id.ToString() ?? string.Empty },
                    { "joinCode", gameSession.JoinCode }
                });

                return View(gameSession);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving game board for session {Id}", id);
                _telemetryClient?.TrackException(ex);
                throw;
            }
        }

        // POST: Session/MarkAnswerCorrect
        [HttpPost]
        public async Task<IActionResult> MarkAnswerCorrect([FromBody] MarkAnswerRequest request)
        {
            if (request == null || !Guid.TryParse(request.RoundId, out var roundId))
            {
                return BadRequest("Invalid request");
            }

            try
            {
                _telemetryClient?.TrackEvent("MarkAnswerCorrect");

                // Get the game round
                var gameRound = await _context.GameRounds
                    .Include(gr => gr.Question)
                    .FirstOrDefaultAsync(gr => gr.Id == roundId);

                if (gameRound == null)
                {
                    return NotFound("Round not found");
                }

                // If a specific answer was marked correct
                if (!string.IsNullOrEmpty(request.RoundAnswerId) && Guid.TryParse(request.RoundAnswerId, out var roundAnswerId))
                {
                    var roundAnswer = await _context.RoundAnswers
                        .Include(ra => ra.SessionPlayer)
                        .FirstOrDefaultAsync(ra => ra.Id == roundAnswerId);

                    if (roundAnswer != null)
                    {
                        // Mark as correct and award points
                        roundAnswer.IsCorrect = true;
                        roundAnswer.PointsEarned = gameRound.Question?.Points ?? 0;

                        // Update session player score
                        if (roundAnswer.SessionPlayer != null)
                        {
                            roundAnswer.SessionPlayer.Score += roundAnswer.PointsEarned;
                        }

                        // Mark round as answered
                        gameRound.Status = GameRoundStatus.Answered;
                        gameRound.EndedAt = DateTime.UtcNow;

                        await _context.SaveChangesAsync();

                        _logger.LogInformation($"Answer marked correct for round {roundId}, player {roundAnswer.SessionPlayer?.PlayerNickname} earned {roundAnswer.PointsEarned} points");

                        return Ok(new
                        {
                            success = true,
                            message = $"{roundAnswer.SessionPlayer?.PlayerNickname} earned {roundAnswer.PointsEarned} points",
                            pointsEarned = roundAnswer.PointsEarned
                        });
                    }
                }
                else
                {
                    // No correct answer - just end the round
                    gameRound.Status = GameRoundStatus.Ended;
                    gameRound.EndedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Round {roundId} ended with no correct answer");

                    return Ok(new
                    {
                        success = true,
                        message = "Round ended. No one earned points.",
                        pointsEarned = 0
                    });
                }

                return BadRequest("No answer specified");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking answer correct");
                _telemetryClient?.TrackException(ex);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Broadcasts session update to all connected clients via SignalR hub
        /// </summary>
        private async Task BroadcastSessionUpdateViaHub(Guid sessionId)
        {
            if (_hubContext == null)
            {
                _logger.LogWarning("Hub context not available for broadcasting session update");
                return;
            }

            try
            {
                // Only include SessionPlayers - avoid expensive PlayerAnswers join
                var gameSession = await _context.GameSessions
                    .Include(s => s.SessionPlayers)
                    .FirstOrDefaultAsync(s => s.Id == sessionId);

                if (gameSession == null)
                {
                    return;
                }

                // Count answered questions from GameRounds instead of PlayerAnswers
                var answeredQuestionsCount = await _context.GameRounds
                    .Where(gr => gr.GameSessionId == sessionId && (gr.Status == GameRoundStatus.Ended || gr.Status == GameRoundStatus.Answered))
                    .CountAsync();

                var sessionData = new
                {
                    sessionId = gameSession.Id,
                    playerCount = gameSession.SessionPlayers?.Count ?? 0,
                    currentPlayerScore = gameSession.SessionPlayers?.FirstOrDefault()?.Score ?? 0,
                    topPlayerScore = gameSession.SessionPlayers?.OrderByDescending(sp => sp.Score).FirstOrDefault()?.Score ?? 0,
                    leaderboard = gameSession.SessionPlayers?
                        .OrderByDescending(sp => sp.Score)
                        .Select((sp, index) => new
                        {
                            rank = index + 1,
                            playerNickname = sp.PlayerNickname,
                            score = sp.Score,
                            joinedAt = sp.JoinedAt.ToString("O")
                        }),
                    timeRemaining = (gameSession.ExpiresAt - DateTime.UtcNow).TotalSeconds,
                    answeredQuestionsCount = answeredQuestionsCount,
                    status = gameSession.Status.ToString()
                };

                // Broadcast to all clients in the session group
                await _hubContext.Clients.Group($"session-{sessionId}").SendAsync("SessionUpdated", sessionData);

                _logger.LogInformation($"Session update broadcasted for session {sessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting session update via hub for session {SessionId}", sessionId);
            }
        }
    }

    // Helper class for MarkAnswerCorrect request
    public class MarkAnswerRequest
    {
        public string? RoundId { get; set; }
        public string? RoundAnswerId { get; set; }
    }
}

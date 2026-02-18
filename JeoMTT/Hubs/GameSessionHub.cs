using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using JeoMTT.Data;
using JeoMTT.Models;
using System.Diagnostics;

namespace JeoMTT.Hubs
{
    /// <summary>
    /// Real-time hub for game session updates using SignalR
    /// Handles:
    /// - Player joining notifications
    /// - Answer submissions and verification
    /// - Score updates
    /// - Session state changes
    /// </summary>
    public class GameSessionHub : Hub
    {
        private readonly JeoGameDbContext _context;
        private readonly ILogger<GameSessionHub> _logger;
        private const int ANSWER_VERIFICATION_DELAY_MS = 2000; // 2-second delay before showing result

        // Track session-connectionId mapping for disconnection handling
        private static readonly Dictionary<string, string> ConnectionToSessionMap = new();
        private static readonly Dictionary<string, string> ConnectionToNicknameMap = new();

        // Track active round timers per session (sessionId -> (timerTask, roundId, expiryTime))
        // This ensures all clients see the same synchronized countdown
        private static readonly Dictionary<string, (Task? timerTask, string? roundId, DateTime expiryTime)> RoundTimers = new();

        public GameSessionHub(JeoGameDbContext context, ILogger<GameSessionHub> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Called when a player joins the session
        /// Adds client to session group and notifies all observers
        /// </summary>
        public async Task JoinSession(string sessionId, string playerNickname)
        {
            try
            {
                // Add this connection to the session group
                await Groups.AddToGroupAsync(Context.ConnectionId, $"session-{sessionId}");
                
                _logger.LogInformation($"Player '{playerNickname}' joined session {sessionId} via SignalR");

                // Notify all clients in the session that a player joined
                await Clients.Group($"session-{sessionId}").SendAsync("PlayerJoined", new
                {
                    playerNickname = playerNickname,
                    joinedAt = DateTime.UtcNow.ToString("O"),
                    message = $"{playerNickname} has joined the session!"
                });

                // Send updated session data to all clients
                await BroadcastSessionUpdate(Guid.Parse(sessionId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in JoinSession for session {SessionId}", sessionId);
                await Clients.Caller.SendAsync("Error", "Failed to join session");
            }
        }        /// <summary>
        /// Admin selects a question to start a new round
        /// Creates a GameRound and sets its status to Active
        /// </summary>
        public async Task SelectQuestion(string sessionId, string questionId)
        {
            try
            {
                if (!Guid.TryParse(sessionId, out var parsedSessionId) || !Guid.TryParse(questionId, out var parsedQuestionId))
                {
                    _logger.LogWarning("Invalid session or question ID format");
                    return;
                }

                // Get the game session to get the next round number
                var gameSession = await _context.GameSessions
                    .Include(s => s.Game)
                    .ThenInclude(g => g!.Categories)
                    .ThenInclude(c => c.Questions)
                    .FirstOrDefaultAsync(s => s.Id == parsedSessionId);

                if (gameSession == null)
                {
                    _logger.LogWarning("Session not found: {SessionId}", parsedSessionId);
                    return;
                }

                // Create a new GameRound
                var gameRound = new GameRound
                {
                    Id = Guid.NewGuid(),
                    GameSessionId = parsedSessionId,
                    QuestionId = parsedQuestionId,
                    RoundNumber = await _context.GameRounds
                        .Where(gr => gr.GameSessionId == parsedSessionId)
                        .CountAsync() + 1,
                    StartedAt = DateTime.UtcNow,
                    Status = GameRoundStatus.Active
                };                _context.GameRounds.Add(gameRound);
                await _context.SaveChangesAsync();

                // Get the question details
                var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == parsedQuestionId);

                // Notify all players and admin that a question has been selected
                await Clients.Group($"session-{sessionId}").SendAsync("QuestionSelected", new
                {
                    roundId = gameRound.Id,
                    questionId = parsedQuestionId,
                    categoryName = question?.Category?.Name,
                    points = question?.Points,
                    questionText = question?.Text,
                    selectedAt = DateTime.UtcNow.ToString("O"),
                    timerSeconds = gameSession.QuestionTimerSeconds
                });

                // Start server-side timer for this round
                await StartRoundTimer(parsedSessionId, gameRound.Id.ToString(), gameSession.QuestionTimerSeconds);

                _logger.LogInformation($"Question {parsedQuestionId} selected for session {parsedSessionId}, round {gameRound.RoundNumber}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting question");
                await Clients.Caller.SendAsync("Error", "Failed to select question");
            }
        }

        /// <summary>
        /// Player submits an answer for the current question
        /// Creates a RoundAnswer record
        /// </summary>
        public async Task SubmitAnswer(string sessionId, string sessionPlayerId, string roundId, string answer)
        {
            try
            {
                if (!Guid.TryParse(sessionId, out var parsedSessionId) || 
                    !Guid.TryParse(sessionPlayerId, out var parsedPlayerId) ||
                    !Guid.TryParse(roundId, out var parsedRoundId))
                {
                    _logger.LogWarning("Invalid session, player, or round ID format");
                    return;
                }

                if (string.IsNullOrWhiteSpace(answer))
                {
                    await Clients.Caller.SendAsync("AnswerValidationError", "Answer cannot be empty");
                    return;
                }

                // Get the player
                var player = await _context.SessionPlayers.FirstOrDefaultAsync(sp => sp.Id == parsedPlayerId);
                if (player == null)
                {
                    _logger.LogWarning("Player not found: {PlayerId}", parsedPlayerId);
                    return;
                }

                // Check if player already answered this round
                var existingAnswer = await _context.RoundAnswers
                    .FirstOrDefaultAsync(ra => ra.GameRoundId == parsedRoundId && ra.SessionPlayerId == parsedPlayerId);

                if (existingAnswer != null)
                {
                    await Clients.Caller.SendAsync("AnswerValidationError", "You have already answered this question");
                    return;
                }

                // Create the RoundAnswer record
                var roundAnswer = new RoundAnswer
                {
                    Id = Guid.NewGuid(),
                    GameRoundId = parsedRoundId,
                    SessionPlayerId = parsedPlayerId,
                    Answer = answer.Trim(),
                    SubmittedAt = DateTime.UtcNow,
                    IsCorrect = false, // Will be marked by admin
                    PointsEarned = 0   // Will be awarded by admin
                };

                _context.RoundAnswers.Add(roundAnswer);
                await _context.SaveChangesAsync();

                // Notify admin that an answer has been submitted
                await Clients.Group($"session-{sessionId}-admin").SendAsync("AnswerSubmitted", new
                {
                    roundAnswerId = roundAnswer.Id,
                    sessionPlayerId = parsedPlayerId,
                    playerNickname = player.PlayerNickname,
                    answer = answer,
                    submittedAt = DateTime.UtcNow.ToString("O")
                });

                _logger.LogInformation($"Player {player.PlayerNickname} submitted answer for session {parsedSessionId}, round {parsedRoundId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting answer");
                await Clients.Caller.SendAsync("Error", "Failed to submit answer");
            }
        }

        /// <summary>
        /// Admin shows the correct answer and ends the answer submission period
        /// </summary>
        public async Task ShowAnswer(string sessionId, string roundId)
        {
            try
            {
                if (!Guid.TryParse(sessionId, out var parsedSessionId) || !Guid.TryParse(roundId, out var parsedRoundId))
                {
                    _logger.LogWarning("Invalid session or round ID format");
                    return;
                }

                // Get the round and question
                var gameRound = await _context.GameRounds
                    .Include(gr => gr.Question)
                    .FirstOrDefaultAsync(gr => gr.Id == parsedRoundId);

                if (gameRound == null)
                {
                    _logger.LogWarning("Round not found: {RoundId}", parsedRoundId);
                    return;
                }

                // Get all answers submitted for this round
                var answers = await _context.RoundAnswers
                    .Include(ra => ra.SessionPlayer)
                    .Where(ra => ra.GameRoundId == parsedRoundId)
                    .OrderBy(ra => ra.SubmittedAt)
                    .ToListAsync();

                // Notify all clients (admin and players) that the answer is shown
                await Clients.Group($"session-{sessionId}").SendAsync("AnswerRevealed", new
                {
                    roundId = parsedRoundId,
                    answer = gameRound.Question?.Answer,
                    submittedAnswers = answers.Select(a => new
                    {
                        roundAnswerId = a.Id,
                        playerNickname = a.SessionPlayer?.PlayerNickname,
                        playerAnswer = a.Answer,
                        submittedAt = a.SubmittedAt.ToString("O")
                    }),
                    revealedAt = DateTime.UtcNow.ToString("O")
                });

                _logger.LogInformation($"Answer revealed for round {parsedRoundId} in session {parsedSessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing answer");
                await Clients.Caller.SendAsync("Error", "Failed to show answer");
            }
        }

        /// <summary>
        /// Admin ends the current round and transitions to answer review
        /// </summary>
        public async Task EndRound(string sessionId, string roundId)
        {
            try
            {
                if (!Guid.TryParse(sessionId, out var parsedSessionId) || !Guid.TryParse(roundId, out var parsedRoundId))
                {
                    _logger.LogWarning("Invalid session or round ID format");
                    return;
                }

                // Update round status to Ended
                var gameRound = await _context.GameRounds.FirstOrDefaultAsync(gr => gr.Id == parsedRoundId);
                if (gameRound != null)
                {
                    gameRound.Status = GameRoundStatus.Ended;
                    _context.GameRounds.Update(gameRound);
                    await _context.SaveChangesAsync();
                }

                // Notify all clients that the round has ended
                await Clients.Group($"session-{sessionId}").SendAsync("RoundEnded", new
                {
                    roundId = parsedRoundId,
                    sessionId = parsedSessionId,
                    endedAt = DateTime.UtcNow.ToString("O")
                });

                _logger.LogInformation($"Round {parsedRoundId} ended for session {parsedSessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending round");
                await Clients.Caller.SendAsync("Error", "Failed to end round");
            }
        }

        /// <summary>
        /// Admin marks an answer as correct/incorrect and awards points
        /// </summary>
        public async Task MarkAnswer(string sessionId, string roundAnswerId, bool isCorrect, int pointsAwarded)
        {
            try
            {
                if (!Guid.TryParse(sessionId, out var parsedSessionId) || !Guid.TryParse(roundAnswerId, out var parsedAnswerId))
                {
                    _logger.LogWarning("Invalid session or answer ID format");
                    return;
                }

                // Get the round answer
                var roundAnswer = await _context.RoundAnswers
                    .Include(ra => ra.GameRound)
                    .Include(ra => ra.SessionPlayer)
                    .FirstOrDefaultAsync(ra => ra.Id == parsedAnswerId);

                if (roundAnswer == null)
                {
                    _logger.LogWarning("Round answer not found: {AnswerId}", parsedAnswerId);
                    return;
                }

                // Update the round answer
                roundAnswer.IsCorrect = isCorrect;
                roundAnswer.PointsEarned = isCorrect ? pointsAwarded : 0;

                _context.RoundAnswers.Update(roundAnswer);

                // Award points to the player if correct
                if (isCorrect && roundAnswer.SessionPlayer != null)
                {
                    roundAnswer.SessionPlayer.Score += pointsAwarded;
                    _context.SessionPlayers.Update(roundAnswer.SessionPlayer);
                }

                await _context.SaveChangesAsync();

                // Notify all admin clients that answer has been marked
                await Clients.Group($"session-{sessionId}-admin").SendAsync("AnswerMarked", new
                {
                    roundAnswerId = parsedAnswerId,
                    playerNickname = roundAnswer.SessionPlayer?.PlayerNickname,
                    isCorrect = isCorrect,
                    pointsAwarded = pointsAwarded,
                    newScore = roundAnswer.SessionPlayer?.Score ?? 0
                });

                // Broadcast updated session data
                await BroadcastSessionUpdate(parsedSessionId);

                _logger.LogInformation($"Answer {roundAnswerId} marked as {(isCorrect ? "CORRECT" : "INCORRECT")} with {pointsAwarded} points");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking answer");
                await Clients.Caller.SendAsync("Error", "Failed to mark answer");
            }
        }

        /// <summary>
        /// Marks a round as answered and prepares for the next round
        /// </summary>
        public async Task MarkRoundAsAnswered(string sessionId, string roundId)
        {
            try
            {
                if (!Guid.TryParse(sessionId, out var parsedSessionId) || !Guid.TryParse(roundId, out var parsedRoundId))
                {
                    _logger.LogWarning("Invalid session or round ID format");
                    return;
                }

                // Update round status to Answered
                var gameRound = await _context.GameRounds.FirstOrDefaultAsync(gr => gr.Id == parsedRoundId);
                if (gameRound != null)
                {
                    gameRound.Status = GameRoundStatus.Answered;
                    _context.GameRounds.Update(gameRound);
                    await _context.SaveChangesAsync();
                }

                // Notify all clients that round is answered
                await Clients.Group($"session-{sessionId}").SendAsync("RoundAnswered", new
                {
                    roundId = parsedRoundId,
                    sessionId = parsedSessionId,
                    markedAt = DateTime.UtcNow.ToString("O")
                });

                _logger.LogInformation($"Round {parsedRoundId} marked as answered");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking round as answered");
                await Clients.Caller.SendAsync("Error", "Failed to mark round as answered");
            }
        }

        /// <summary>
        /// Add connection to admin group for the session
        /// </summary>
        public async Task JoinAsAdmin(string sessionId)
        {
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"session-{sessionId}-admin");
                _logger.LogInformation($"Admin joined session {sessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining as admin");
                await Clients.Caller.SendAsync("Error", "Failed to join as admin");
            }
        }

        /// <summary>
        /// Broadcasts session update to all clients in the session group
        /// Includes current scores, available questions, and session state
        /// </summary>
        private async Task BroadcastSessionUpdate(Guid sessionId)
        {
            try
            {                var gameSession = await _context.GameSessions
                    .Include(s => s.SessionPlayers)
                    .FirstOrDefaultAsync(s => s.Id == sessionId);

                if (gameSession == null)
                    return;

                var sessionData = new
                {
                    sessionId = gameSession.Id,
                    playerCount = gameSession.SessionPlayers.Count,
                    currentPlayerScore = gameSession.SessionPlayers.FirstOrDefault()?.Score ?? 0,
                    topPlayerScore = gameSession.SessionPlayers.OrderByDescending(sp => sp.Score).FirstOrDefault()?.Score ?? 0,
                    leaderboard = gameSession.SessionPlayers
                        .OrderByDescending(sp => sp.Score)
                        .Select((sp, index) => new
                        {
                            rank = index + 1,
                            playerNickname = sp.PlayerNickname,
                            score = sp.Score,                    joinedAt = sp.JoinedAt.ToString("O")
                        }),
                    timeRemaining = (gameSession.ExpiresAt - DateTime.UtcNow).TotalSeconds,
                    answeredQuestionsCount = gameSession.Rounds?.Count ?? 0,
                    status = gameSession.Status.ToString()
                };

                await Clients.Group($"session-{sessionId}").SendAsync("SessionUpdated", sessionData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting session update for session {SessionId}", sessionId);
            }
        }        /// <summary>
        /// Handles client disconnection
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception != null)
            {
                _logger.LogError(exception, "Client disconnected with error");
            }
            
            _logger.LogInformation($"Client {Context.ConnectionId} disconnected");
            
            // Notify all session groups about the disconnection
            await BroadcastAllSessionUpdates();
            
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Broadcasts updated session data to all connected clients in all sessions
        /// Used when a player disconnects
        /// </summary>
        private async Task BroadcastAllSessionUpdates()
        {
            try
            {
                // Get all active sessions
                var activeSessions = await _context.GameSessions
                    .Where(s => s.Status == GameSessionStatus.Active)                    .Include(s => s.SessionPlayers)
                    .ToListAsync();

                foreach (var gameSession in activeSessions)
                {
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
                            }),                        timeRemaining = (gameSession.ExpiresAt - DateTime.UtcNow).TotalSeconds,
                        answeredQuestionsCount = gameSession.Rounds?.Count ?? 0,
                        status = gameSession.Status.ToString()
                    };

                    // Broadcast to all clients in the session group
                    await Clients.Group($"session-{gameSession.Id}").SendAsync("SessionUpdated", sessionData);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting session updates on disconnection");
            }
        }        /// <summary>
        /// Start a server-side timer for a round that broadcasts remaining time to all clients
        /// Ensures all players and admins see the same synchronized countdown
        /// </summary>
        private async Task StartRoundTimer(Guid sessionId, string roundId, int timerSeconds)
        {
            try
            {
                // Cancel any existing timer for this session
                if (RoundTimers.TryGetValue(sessionId.ToString(), out var existingTimer))
                {
                    // Timer will be replaced, no need to cleanup here
                }

                var expiryTime = DateTime.UtcNow.AddSeconds(timerSeconds);
                var sessionIdStr = sessionId.ToString();

                // Create the timer task - fire and forget pattern
                var timerTask = Task.Run(async () =>
                {
                    try
                    {
                        while (DateTime.UtcNow < expiryTime)
                        {
                            var remainingSeconds = Math.Max(0, (int)(expiryTime - DateTime.UtcNow).TotalSeconds);

                            // Broadcast timer update to all clients in the session
                            await Clients.Group($"session-{sessionIdStr}").SendAsync("TimerUpdate", new
                            {
                                roundId = roundId,
                                remainingSeconds = remainingSeconds,
                                timestamp = DateTime.UtcNow.ToString("O")
                            });

                            if (remainingSeconds <= 0)
                            {
                                break;
                            }

                            // Update every 100ms for smooth countdown
                            await Task.Delay(100);
                        }

                        // Timer expired - notify all clients to show answer
                        await Clients.Group($"session-{sessionIdStr}").SendAsync("TimerExpired", new
                        {
                            roundId = roundId,
                            expiredAt = DateTime.UtcNow.ToString("O")
                        });

                        _logger.LogInformation($"Timer expired for round {roundId} in session {sessionIdStr}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error in round timer for session {sessionIdStr}");
                    }
                });

                // Store the timer task
                RoundTimers[sessionIdStr] = (timerTask, roundId, expiryTime);

                // Await the timer start (fire and forget)
                await Task.Yield();

                _logger.LogInformation($"Started server-side timer for round {roundId} in session {sessionIdStr} ({timerSeconds} seconds)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error starting round timer for session {sessionId}");
            }
        }
    }
}

using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JeoMTT.Data;
using JeoMTT.Models;

namespace JeoMTT.Controllers
{
    public class JeoGameController : Controller
    {
        private readonly JeoGameDbContext _context;
        private readonly TelemetryClient _telemetryClient;
        private readonly ILogger<JeoGameController> _logger;

        public JeoGameController(
            JeoGameDbContext context,
            TelemetryClient telemetryClient,
            ILogger<JeoGameController> logger)
        {
            _context = context;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }        // GET: JeoGame
        public async Task<IActionResult> GameList()
        {
            try
            {
                _telemetryClient.TrackEvent("JeoGameIndexRequested");

                var games = await _context.JeoGames.ToListAsync();
                
                _telemetryClient.TrackEvent("JeoGamesRetrieved", new Dictionary<string, string>
                {
                    { "count", games.Count.ToString() }
                });

                return View(games);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving JeoGames");
                _telemetryClient.TrackException(ex);
                throw;
            }
        }        // GET: JeoGame/GameDetails/5
        public async Task<IActionResult> GameDetails(Guid? id)
        {
            if (id == null || id == Guid.Empty)
            {
                return NotFound();
            }

            try
            {
                var jeoGame = await _context.JeoGames
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (jeoGame == null)
                {
                    _telemetryClient.TrackEvent("JeoGameNotFound", new Dictionary<string, string>
                    {
                        { "id", id.ToString() }
                    });
                    return NotFound();
                }

                _telemetryClient.TrackEvent("JeoGameDetailsRetrieved", new Dictionary<string, string>
                {
                    { "id", id.ToString() },
                    { "name", jeoGame.Name }
                });

                return View(jeoGame);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving JeoGame with id {Id}", id);
                _telemetryClient.TrackException(ex);
                throw;
            }
        }

        // GET: JeoGame/PlayBoard/5
        public async Task<IActionResult> PlayBoard(Guid? id)
        {
            if (id == null || id == Guid.Empty)
            {
                return NotFound();
            }

            try
            {
                var jeoGame = await _context.JeoGames
                    .Include(g => g.Categories)
                    .ThenInclude(c => c.Questions)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (jeoGame == null)
                {
                    return NotFound();
                }

                _telemetryClient.TrackEvent("JeoGamePlayBoardRequested", new Dictionary<string, string>
                {
                    { "gameId", id.ToString() },
                    { "categoryCount", jeoGame.Categories.Count.ToString() }
                });

                return View(jeoGame);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading play board for game {Id}", id);
                _telemetryClient.TrackException(ex);
                throw;
            }
        }

        // POST: JeoGame/SaveQuestion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveQuestion(Guid categoryId, int points, string questionText, string answerText)
        {
            if (string.IsNullOrWhiteSpace(questionText) || string.IsNullOrWhiteSpace(answerText))
            {
                return BadRequest("Question and answer are required");
            }

            if (questionText.Length > 200)
            {
                return BadRequest("Question text cannot exceed 200 characters");
            }

            try
            {
                var category = await _context.Categories.FindAsync(categoryId);
                if (category == null)
                {
                    return NotFound();
                }

                // Check if question with same points already exists
                var existingQuestion = await _context.Questions
                    .FirstOrDefaultAsync(q => q.CategoryId == categoryId && q.Points == points);

                if (existingQuestion != null)
                {
                    // Update existing question
                    existingQuestion.Text = questionText;
                    existingQuestion.Answer = answerText;
                    _context.Questions.Update(existingQuestion);
                }
                else
                {
                    // Create new question
                    var question = new Question
                    {
                        Text = questionText,
                        Answer = answerText,
                        Points = points,
                        CategoryId = categoryId,
                        CreatedAt = DateTime.Now
                    };
                    _context.Questions.Add(question);
                }

                await _context.SaveChangesAsync();

                _telemetryClient.TrackEvent("JeoGameQuestionSaved", new Dictionary<string, string>
                {
                    { "categoryId", categoryId.ToString() },
                    { "points", points.ToString() }
                });

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving question for category {CategoryId}", categoryId);
                _telemetryClient.TrackException(ex);
                return StatusCode(500, "Error saving question");
            }
        }

        // GET: JeoGame/GetQuestion
        [HttpGet]
        public async Task<IActionResult> GetQuestion(Guid categoryId, int points)
        {
            try
            {
                var question = await _context.Questions
                    .FirstOrDefaultAsync(q => q.CategoryId == categoryId && q.Points == points);

                if (question == null)
                {
                    return Ok(new { text = "", answer = "" });
                }

                return Ok(new { text = question.Text, answer = question.Answer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving question for category {CategoryId} and points {Points}", categoryId, points);
                return StatusCode(500, "Error retrieving question");
            }
        }        // GET: JeoGame/CreateGame
        public IActionResult CreateGame()
        {
            _telemetryClient.TrackEvent("JeoGameCreatePageRequested");
            return View();
        }

        // POST: JeoGame/CreateGame
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGame([Bind("Name,Description,Author")] JeoGame jeoGame)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    jeoGame.CreatedAt = DateTime.Now;
                    _context.Add(jeoGame);
                    await _context.SaveChangesAsync();

                    _telemetryClient.TrackEvent("JeoGameCreated", new Dictionary<string, string>
                    {
                        { "id", jeoGame.Id.ToString() },
                        { "name", jeoGame.Name },
                        { "author", jeoGame.Author }
                    });

                    // Redirect to Categories management page
                    return RedirectToAction(nameof(ManageCategories), new { id = jeoGame.Id });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating JeoGame");
                    _telemetryClient.TrackException(ex);
                    throw;
                }
            }

            return View(jeoGame);
        }        // GET: JeoGame/EditGame/5
        public async Task<IActionResult> EditGame(Guid? id)
        {
            if (id == null || id == Guid.Empty)
            {
                return NotFound();
            }

            try
            {
                var jeoGame = await _context.JeoGames.FindAsync(id);
                if (jeoGame == null)
                {
                    return NotFound();
                }

                _telemetryClient.TrackEvent("JeoGameEditPageRequested", new Dictionary<string, string>
                {
                    { "id", id.ToString() }
                });

                return View(jeoGame);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading JeoGame for edit with id {Id}", id);
                _telemetryClient.TrackException(ex);
                throw;
            }
        }        // POST: JeoGame/EditGame/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditGame(Guid id, [Bind("Id,Name,Description,Author,CreatedAt")] JeoGame jeoGame)
        {
            if (id != jeoGame.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(jeoGame);
                    await _context.SaveChangesAsync();                    _telemetryClient.TrackEvent("JeoGameUpdated", new Dictionary<string, string>
                    {
                        { "id", jeoGame.Id.ToString() },
                        { "name", jeoGame.Name }
                    });

                    return RedirectToAction(nameof(GameList));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating JeoGame with id {Id}", id);
                    _telemetryClient.TrackException(ex);
                    throw;
                }
            }

            return View(jeoGame);
        }        // GET: JeoGame/DeleteGame/5
        public async Task<IActionResult> DeleteGame(Guid? id)
        {
            if (id == null || id == Guid.Empty)
            {
                return NotFound();
            }

            try
            {
                var jeoGame = await _context.JeoGames
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (jeoGame == null)
                {
                    return NotFound();
                }

                _telemetryClient.TrackEvent("JeoGameDeletePageRequested", new Dictionary<string, string>
                {
                    { "id", id.ToString() },
                    { "name", jeoGame.Name }
                });

                return View(jeoGame);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading JeoGame for delete with id {Id}", id);
                _telemetryClient.TrackException(ex);
                throw;
            }
        }

        // POST: JeoGame/Delete/5        [HttpPost, ActionName("DeleteGame")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            try
            {
                var jeoGame = await _context.JeoGames.FindAsync(id);
                if (jeoGame != null)
                {
                    _context.JeoGames.Remove(jeoGame);
                    await _context.SaveChangesAsync();

                    _telemetryClient.TrackEvent("JeoGameDeleted", new Dictionary<string, string>
                    {
                        { "id", id.ToString() },
                        { "name", jeoGame.Name }
                    });
                }

                return RedirectToAction(nameof(GameList));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting JeoGame with id {Id}", id);
                _telemetryClient.TrackException(ex);
                throw;
            }
        }

        // GET: JeoGame/ManageCategories/5
        public async Task<IActionResult> ManageCategories(Guid? id)
        {
            if (id == null || id == Guid.Empty)
            {
                return NotFound();
            }

            try
            {
                var jeoGame = await _context.JeoGames
                    .Include(g => g.Categories)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (jeoGame == null)
                {
                    return NotFound();
                }

                _telemetryClient.TrackEvent("JeoGameManageCategoriesRequested", new Dictionary<string, string>
                {
                    { "gameId", id.ToString() },
                    { "gameName", jeoGame.Name }
                });

                return View(jeoGame);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading categories management for game {Id}", id);
                _telemetryClient.TrackException(ex);
                throw;
            }
        }

        // POST: JeoGame/AddCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory(Guid gameId, string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return BadRequest("Category name is required");
            }

            try
            {
                var jeoGame = await _context.JeoGames
                    .Include(g => g.Categories)
                    .FirstOrDefaultAsync(g => g.Id == gameId);

                if (jeoGame == null)
                {
                    return NotFound();
                }

                // Check if game already has 5 categories
                if (jeoGame.Categories.Count >= 5)
                {
                    _telemetryClient.TrackEvent("JeoGameMaxCategoriesReached", new Dictionary<string, string>
                    {
                        { "gameId", gameId.ToString() }
                    });
                    return BadRequest("Maximum 5 categories allowed per game");
                }

                // Check if category name already exists
                if (jeoGame.Categories.Any(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest("Category already exists");
                }

                var category = new Category
                {
                    Name = categoryName,
                    JeoGameId = gameId
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                _telemetryClient.TrackEvent("JeoGameCategoryAdded", new Dictionary<string, string>
                {
                    { "gameId", gameId.ToString() },
                    { "categoryName", categoryName }
                });

                return Ok(new { id = category.Id, name = category.Name });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding category to game {GameId}", gameId);
                _telemetryClient.TrackException(ex);
                return StatusCode(500, "Error adding category");
            }
        }

        // POST: JeoGame/RemoveCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveCategory(Guid categoryId, Guid gameId)
        {
            try
            {
                var category = await _context.Categories.FindAsync(categoryId);

                if (category == null || category.JeoGameId != gameId)
                {
                    return NotFound();
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                _telemetryClient.TrackEvent("JeoGameCategoryRemoved", new Dictionary<string, string>
                {
                    { "gameId", gameId.ToString() },
                    { "categoryId", categoryId.ToString() }
                });

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing category {CategoryId}", categoryId);
                _telemetryClient.TrackException(ex);
                return StatusCode(500, "Error removing category");
            }
        }

        // POST: JeoGame/FinishCategories
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinishCategories(Guid id)
        {
            try
            {
                var jeoGame = await _context.JeoGames
                    .Include(g => g.Categories)
                    .FirstOrDefaultAsync(g => g.Id == id);

                if (jeoGame == null)
                {
                    return NotFound();
                }

                _telemetryClient.TrackEvent("JeoGameCategoriesFinished", new Dictionary<string, string>
                {
                    { "gameId", id.ToString() },
                    { "categoryCount", jeoGame.Categories.Count.ToString() }
                });                return RedirectToAction(nameof(PlayBoard), new { id = jeoGame.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finishing categories for game {Id}", id);
                _telemetryClient.TrackException(ex);
                throw;
            }
        }

        // GET: JeoGame/GetGamesJson
        [HttpGet]
        public async Task<IActionResult> GetGamesJson()
        {
            try
            {
                var games = await _context.JeoGames
                    .Select(g => new { id = g.Id, name = g.Name })
                    .ToListAsync();

                return Json(games);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving games for JSON");
                _telemetryClient.TrackException(ex);
                return StatusCode(500, "Error retrieving games");
            }
        }
    }
}

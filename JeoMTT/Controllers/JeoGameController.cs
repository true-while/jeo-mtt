using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JeoMTT.Data;
using JeoMTT.Models;

namespace JeoMTT.Controllers
{
    [Authorize]
    public class JeoGameController : Controller
    {
        private readonly JeoGameDbContext _context;
        private readonly TelemetryClient _telemetryClient;
        private readonly ILogger<JeoGameController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public JeoGameController(
            JeoGameDbContext context,
            TelemetryClient telemetryClient,
            ILogger<JeoGameController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _telemetryClient = telemetryClient;
            _logger = logger;
            _userManager = userManager;
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
                    .Include(g => g.Categories.OrderBy(c => c.DisplayOrder))
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
        public async Task<IActionResult> CreateGame([Bind("Name,Description")] JeoGame jeoGame)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Get the current user and set Author from their Nickname
                    var user = await _userManager.GetUserAsync(User);
                    jeoGame.Author = user?.Nickname ?? "Anonymous";
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
        }

        // POST: JeoGame/EditGame/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditGame(Guid id, [Bind("Id,Name,Description,CreatedAt")] JeoGame jeoGame)
        {
            if (id != jeoGame.Id)
            {
                return NotFound();
            }

            // Check which button was clicked
            var buttonClicked = Request.Form["button"].FirstOrDefault();

            try
            {
                // Get current user to set as Author
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    ModelState.AddModelError("", "User not found. Please log in again.");
                    return View(jeoGame);
                }

                // Get existing game to preserve fields
                var existingGame = await _context.JeoGames.FindAsync(id);
                if (existingGame == null)
                {
                    return NotFound();
                }

                // Clear Author validation errors since we set it server-side
                ModelState.Remove("Author");

                // Update the fields from the form
                existingGame.Name = jeoGame.Name;
                existingGame.Description = jeoGame.Description;
                // Auto-populate Author from current user's nickname
                existingGame.Author = currentUser.Nickname ?? currentUser.UserName ?? "Unknown";
                // CreatedAt is preserved

                // Manually validate the updated model
                if (string.IsNullOrWhiteSpace(existingGame.Name) || existingGame.Name.Length > 100)
                {
                    ModelState.AddModelError("Name", "Game name must be between 1 and 100 characters");
                }
                
                if (!string.IsNullOrEmpty(existingGame.Description) && existingGame.Description.Length > 500)
                {
                    ModelState.AddModelError("Description", "Description cannot exceed 500 characters");
                }

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState invalid for EditGame. Errors: {Errors}", 
                        string.Join(", ", ModelState.Values.SelectMany(v => v.Errors)));
                    return View(existingGame);
                }

                _context.Update(existingGame);
                await _context.SaveChangesAsync();
                _telemetryClient.TrackEvent("JeoGameUpdated", new Dictionary<string, string>
                {
                    { "id", existingGame.Id.ToString() },
                    { "name", existingGame.Name },
                    { "author", existingGame.Author }
                });
                
                if (buttonClicked == "next")
                {
                    return RedirectToAction("ManageCategories", new { id = existingGame.Id });
                }
                return RedirectToAction(nameof(GameList));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating JeoGame with id {Id}", id);
                _telemetryClient.TrackException(ex);
                ModelState.AddModelError("", "An error occurred while saving. Please try again.");
                var existingGame = await _context.JeoGames.FindAsync(id);
                return View(existingGame);
            }
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
                    .Include(g => g.Categories.OrderBy(c => c.DisplayOrder))
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
                    return BadRequest($"Group name \"{categoryName}\" already exists. Please use a different name.");
                }

                var category = new Category
                {
                    Name = categoryName,
                    JeoGameId = gameId,
                    DisplayOrder = jeoGame.Categories.Count + 1
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

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing category from game {GameId}", gameId);
                _telemetryClient.TrackException(ex);
                return StatusCode(500, "Error removing category");
            }
        }

        // POST: JeoGame/ReorderCategories
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReorderCategories(Guid gameId, List<Guid> categoryOrder)
        {
            try
            {
                _logger.LogWarning("ReorderCategories called with gameId: {GameId}, order count: {Count}", 
                    gameId, categoryOrder?.Count ?? 0);

                if (categoryOrder == null || categoryOrder.Count == 0)
                {
                    return BadRequest("Category order is required");
                }

                var categories = await _context.Categories
                    .Where(c => c.JeoGameId == gameId && categoryOrder.Contains(c.Id))
                    .ToListAsync();

                _logger.LogWarning("Found {Count} categories in database", categories.Count);

                for (int i = 0; i < categoryOrder.Count; i++)
                {
                    var category = categories.FirstOrDefault(c => c.Id == categoryOrder[i]);
                    if (category != null)
                    {
                        category.DisplayOrder = i + 1;
                        _logger.LogWarning("Updated category {CategoryId} to DisplayOrder {DisplayOrder}", 
                            category.Id, category.DisplayOrder);
                    }
                }

                await _context.SaveChangesAsync();

                _telemetryClient.TrackEvent("JeoGameCategoriesReordered", new Dictionary<string, string>
                {
                    { "gameId", gameId.ToString() },
                    { "count", categoryOrder.Count.ToString() }
                });

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering categories for game {GameId}", gameId);
                _telemetryClient.TrackException(ex);
                return StatusCode(500, "Error reordering categories");
            }
        }

        // POST: JeoGame/SaveAndReturnToGames
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAndReturnToGames(Guid gameId)
        {
            try
            {
                // Get all categories for this game and save their order
                var categories = await _context.Categories
                    .Where(c => c.JeoGameId == gameId)
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync();

                if (categories.Count > 0)
                {
                    // Ensure all categories have proper display order
                    for (int i = 0; i < categories.Count; i++)
                    {
                        categories[i].DisplayOrder = i + 1;
                    }

                    await _context.SaveChangesAsync();

                    _telemetryClient.TrackEvent("JeoGameCategoriesSaved", new Dictionary<string, string>
                    {
                        { "gameId", gameId.ToString() },
                        { "count", categories.Count.ToString() }
                    });
                }

                return RedirectToAction(nameof(GameList));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving categories for game {GameId}", gameId);
                _telemetryClient.TrackException(ex);
                return StatusCode(500, "Error saving categories");
            }
        }

        // POST: JeoGame/SaveAndContinueToBoard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAndContinueToBoard(Guid gameId)
        {
            try
            {
                // Get all categories for this game and save their order
                var categories = await _context.Categories
                    .Where(c => c.JeoGameId == gameId)
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync();

                if (categories.Count > 0)
                {
                    // Ensure all categories have proper display order
                    for (int i = 0; i < categories.Count; i++)
                    {
                        categories[i].DisplayOrder = i + 1;
                    }

                    await _context.SaveChangesAsync();

                    _telemetryClient.TrackEvent("JeoGameCategoriesSavedAndContinued", new Dictionary<string, string>
                    {
                        { "gameId", gameId.ToString() },
                        { "count", categories.Count.ToString() }
                    });
                }

                return RedirectToAction(nameof(PlayBoard), new { id = gameId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving categories and continuing to board for game {GameId}", gameId);
                _telemetryClient.TrackException(ex);
                return StatusCode(500, "Error saving categories");
            }
        }

        // POST: JeoGame/SaveAndReturn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAndReturn(Guid id)
        {
            try
            {
                var jeoGame = await _context.JeoGames
                    .Include(g => g.Categories.OrderBy(c => c.DisplayOrder))
                    .ThenInclude(c => c.Questions)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (jeoGame == null)
                {
                    return NotFound();
                }

                // Save all changes to the database (questions that were edited via modal)
                await _context.SaveChangesAsync();

                _telemetryClient.TrackEvent("JeoGameSavedFromPlayBoard", new Dictionary<string, string>
                {
                    { "gameId", id.ToString() }
                });

                return RedirectToAction(nameof(GameList));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving game {GameId} from playboard", id);
                _telemetryClient.TrackException(ex);
                return StatusCode(500, "Error saving game");
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
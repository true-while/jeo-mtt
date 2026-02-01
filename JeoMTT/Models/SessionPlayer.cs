namespace JeoMTT.Models
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;    /// <summary>
    /// Represents a player participating in a game session.
    /// Tracks individual player scores and performance within a session.
    /// This is the only player class used in the application.
    /// Player data is ephemeral - only exists during the session and is archived with the session.
    /// </summary>
    public class SessionPlayer
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid GameSessionId { get; set; }

        [StringLength(50, ErrorMessage = "Nickname must be between 1 and 50 characters")]
        [Display(Name = "Player Nickname")]
        public string PlayerNickname { get; set; } = string.Empty;        [Range(0, int.MaxValue, ErrorMessage = "Score cannot be negative")]
        [Display(Name = "Score")]
        public int Score { get; set; } = 0;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        // Relationships
        [ForeignKey("GameSessionId")]
        public GameSession? GameSession { get; set; }
    }
}

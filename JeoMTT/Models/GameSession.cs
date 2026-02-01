namespace JeoMTT.Models
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class GameSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid GameId { get; set; }

        [StringLength(10, ErrorMessage = "Join code must be 10 characters")]
        [Display(Name = "Join Code")]
        public string JoinCode { get; set; } = GenerateJoinCode();

        [Display(Name = "Status")]
        public GameSessionStatus Status { get; set; } = GameSessionStatus.Active;

        // Use UTC timestamps - no default initializers to prevent early evaluation
        public DateTime StartedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? CompletedAt { get; set; }        [Range(0, int.MaxValue, ErrorMessage = "Timer cannot be negative")]
        [Display(Name = "Question Timer (seconds)")]
        public int QuestionTimerSeconds { get; set; } = 30;

        [StringLength(100, ErrorMessage = "Session name must be 100 characters or less")]
        [Display(Name = "Session Name")]
        public string SessionName { get; set; } = string.Empty;

        private static string GenerateJoinCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Range(0, 10)
                .Select(_ => chars[random.Next(chars.Length)])
                .ToArray());
        }        // Relationships
        [ForeignKey("GameId")]
        public JeoGame? Game { get; set; }

        public ICollection<SessionPlayer> SessionPlayers { get; set; } = new List<SessionPlayer>();
        public ICollection<GameRound> Rounds { get; set; } = new List<GameRound>();
    }

    public enum GameSessionStatus
    {
        Active = 0,
        Archived = 1
    }
}

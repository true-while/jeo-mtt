namespace JeoMTT.Models
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;    /// <summary>
    /// Tracks the current round state during a game session
    /// </summary>
    public class GameRound
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid GameSessionId { get; set; }

        public Guid QuestionId { get; set; }

        [Display(Name = "Round Number")]
        public int RoundNumber { get; set; }        [Display(Name = "Start Time")]
        public DateTime StartedAt { get; set; }

        [Display(Name = "End Time")]
        public DateTime? EndedAt { get; set; }

        [Display(Name = "Status")]
        public GameRoundStatus Status { get; set; } = GameRoundStatus.Pending;

        // Relationships
        [ForeignKey("GameSessionId")]
        public GameSession? GameSession { get; set; }

        [ForeignKey("QuestionId")]
        public Question? Question { get; set; }

        public ICollection<RoundAnswer> Answers { get; set; } = new List<RoundAnswer>();
    }

    /// <summary>
    /// Tracks answers submitted by players during a round
    /// </summary>
    public class RoundAnswer
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid GameRoundId { get; set; }

        public Guid SessionPlayerId { get; set; }

        [StringLength(500, ErrorMessage = "Answer cannot exceed 500 characters")]
        [Display(Name = "Answer")]
        public string Answer { get; set; } = string.Empty;

        [Display(Name = "Submitted At")]
        public DateTime SubmittedAt { get; set; }

        [Display(Name = "Is Correct")]
        public bool IsCorrect { get; set; }

        [Display(Name = "Points Earned")]
        public int PointsEarned { get; set; }

        // Relationships
        [ForeignKey("GameRoundId")]
        public GameRound? GameRound { get; set; }

        [ForeignKey("SessionPlayerId")]
        public SessionPlayer? SessionPlayer { get; set; }
    }

    public enum GameRoundStatus
    {
        Pending = 0,
        Active = 1,
        Ended = 2,
        Answered = 3
    }
}

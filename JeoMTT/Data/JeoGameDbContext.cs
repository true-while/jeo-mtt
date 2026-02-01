using Microsoft.EntityFrameworkCore;
using JeoMTT.Models;

namespace JeoMTT.Data
{
    public class JeoGameDbContext : DbContext
    {
        public JeoGameDbContext(DbContextOptions<JeoGameDbContext> options) 
            : base(options)
        {
        }          public DbSet<JeoGame> JeoGames { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<GameSession> GameSessions { get; set; }
        public DbSet<SessionPlayer> SessionPlayers { get; set; }
        public DbSet<GameRound> GameRounds { get; set; }
        public DbSet<RoundAnswer> RoundAnswers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Configure JeoGame entity
            modelBuilder.Entity<JeoGame>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Author).HasMaxLength(255);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                
                // Relationship: One JeoGame has many Categories
                entity.HasMany(e => e.Categories)
                    .WithOne(c => c.JeoGame)
                    .HasForeignKey(c => c.JeoGameId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Category entity
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.JeoGameId).IsRequired();
                
                // Unique constraint: Category name must be unique per game
                entity.HasIndex(e => new { e.JeoGameId, e.Name }).IsUnique();
                
                // Relationship: One Category has many Questions
                entity.HasMany(e => e.Questions)
                    .WithOne(q => q.Category)
                    .HasForeignKey(q => q.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });            
            // Configure Question entity
            modelBuilder.Entity<Question>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Text).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.Answer).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.Points).IsRequired();
                entity.Property(e => e.CategoryId).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                
                // Validate points are between 100 and 500
                entity.HasCheckConstraint("CK_Question_Points", "[Points] IN (100, 200, 300, 400, 500)");            
            });

            // Configure GameSession entity
            modelBuilder.Entity<GameSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.GameId).IsRequired();
                entity.Property(e => e.JoinCode).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.StartedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.ExpiresAt).IsRequired();
                entity.Property(e => e.QuestionTimerSeconds).IsRequired().HasDefaultValue(30);                
                // Make JoinCode unique
                entity.HasIndex(e => e.JoinCode).IsUnique();

                // Foreign key to JeoGame
                entity.HasOne(e => e.Game)
                    .WithMany()
                    .HasForeignKey(e => e.GameId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Relationship: One GameSession has many SessionPlayers
                entity.HasMany(e => e.SessionPlayers)
                    .WithOne(sp => sp.GameSession)                    .HasForeignKey(sp => sp.GameSessionId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Relationship: One GameSession has many GameRounds
                entity.HasMany(e => e.Rounds)
                    .WithOne(gr => gr.GameSession)
                    .HasForeignKey(gr => gr.GameSessionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            // Configure SessionPlayer entity
            modelBuilder.Entity<SessionPlayer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.GameSessionId).IsRequired();
                entity.Property(e => e.PlayerNickname).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Score).IsRequired().HasDefaultValue(0);
                entity.Property(e => e.JoinedAt).HasDefaultValueSql("GETDATE()");

                // Unique constraint: One nickname per session
                entity.HasIndex(e => new { e.GameSessionId, e.PlayerNickname }).IsUnique();            });            
            // NOTE: PlayerAnswer entity removed - using GameRound and RoundAnswer for round-based game flow

            // Configure GameRound entity
            modelBuilder.Entity<GameRound>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.GameSessionId).IsRequired();
                entity.Property(e => e.QuestionId).IsRequired();
                entity.Property(e => e.RoundNumber).IsRequired();
                entity.Property(e => e.StartedAt).IsRequired();
                entity.Property(e => e.Status).IsRequired();

                // Foreign key to GameSession
                entity.HasOne(e => e.GameSession)
                    .WithMany()
                    .HasForeignKey(e => e.GameSessionId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Foreign key to Question
                entity.HasOne(e => e.Question)
                    .WithMany()
                    .HasForeignKey(e => e.QuestionId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Relationship: One GameRound has many RoundAnswers
                entity.HasMany(e => e.Answers)
                    .WithOne(ra => ra.GameRound)
                    .HasForeignKey(ra => ra.GameRoundId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure RoundAnswer entity
            modelBuilder.Entity<RoundAnswer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.GameRoundId).IsRequired();
                entity.Property(e => e.SessionPlayerId).IsRequired();
                entity.Property(e => e.Answer).IsRequired().HasMaxLength(500);
                entity.Property(e => e.SubmittedAt).IsRequired();
                entity.Property(e => e.IsCorrect).IsRequired();
                entity.Property(e => e.PointsEarned).IsRequired();

                // Foreign key to SessionPlayer
                entity.HasOne(e => e.SessionPlayer)
                    .WithMany()
                    .HasForeignKey(e => e.SessionPlayerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}

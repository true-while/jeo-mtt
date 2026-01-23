using Microsoft.EntityFrameworkCore;
using JeoMTT.Models;

namespace JeoMTT.Data
{
    public class JeoGameDbContext : DbContext
    {
        public JeoGameDbContext(DbContextOptions<JeoGameDbContext> options) 
            : base(options)
        {
        }

        public DbSet<JeoGame> JeoGames { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Question> Questions { get; set; }

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
        }
    }
}

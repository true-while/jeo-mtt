namespace JeoMTT.Models
{
    public class JeoGame
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Relationships
        public ICollection<Category> Categories { get; set; } = new List<Category>();
    }

    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int JeoGameId { get; set; }

        // Relationships
        public JeoGame? JeoGame { get; set; }
        public ICollection<Question> Questions { get; set; } = new List<Question>();
    }

    public class Question
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public int Points { get; set; } // 100, 200, 300, 400, or 500
        public int CategoryId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Relationships
        public Category? Category { get; set; }
    }
}

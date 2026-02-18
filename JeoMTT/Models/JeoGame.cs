namespace JeoMTT.Models
{
    using System.ComponentModel.DataAnnotations;    public class JeoGame
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required(ErrorMessage = "Game name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Game name must be between 1 and 100 characters")]
        [Display(Name = "Game Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Author name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Author name must be between 1 and 100 characters")]
        [Display(Name = "Author")]
        public string Author { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Relationships
        public ICollection<Category> Categories { get; set; } = new List<Category>();
    }    public class Category
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required(ErrorMessage = "Category name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Category name must be between 1 and 100 characters")]
        [Display(Name = "Category Name")]
        public string Name { get; set; } = string.Empty;

        public Guid JeoGameId { get; set; }
        
        public int DisplayOrder { get; set; } = 0;

        // Relationships
        public JeoGame? JeoGame { get; set; }
        public ICollection<Question> Questions { get; set; } = new List<Question>();
    }    public class Question
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required(ErrorMessage = "Question text is required")]
        [StringLength(500, MinimumLength = 1, ErrorMessage = "Question text must be between 1 and 500 characters")]
        [Display(Name = "Question")]
        public string Text { get; set; } = string.Empty;

        [Required(ErrorMessage = "Answer is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Answer must be between 1 and 100 characters")]
        [Display(Name = "Answer")]
        public string Answer { get; set; } = string.Empty;

        [Required(ErrorMessage = "Points value is required")]
        [Range(100, 500, ErrorMessage = "Points must be one of: 100, 200, 300, 400, or 500")]
        [Display(Name = "Points")]
        public int Points { get; set; } // 100, 200, 300, 400, or 500

        public Guid CategoryId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Relationships
        public Category? Category { get; set; }
    }
}

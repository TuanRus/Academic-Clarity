namespace ScientificTrendTracker.Models.Entities
{
    public class Author
    {
        [Key]
        public int AuthorId { get; set; }

        [MaxLength(100)]
        public string OpenAlexId { get; set; }

        [Required]
        [MaxLength(255)]
        public string FullName { get; set; }

        [MaxLength(255)]
        public string Affiliation { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PaperAuthor> PaperAuthors { get; set; }
    }
}

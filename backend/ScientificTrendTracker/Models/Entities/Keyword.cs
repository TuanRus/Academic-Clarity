namespace ScientificTrendTracker.Models.Entities
{
    public class Keyword
    {
        [Key]
        [MaxLength(50)]
        public string KeywordId { get; set; }

        [Required]
        [MaxLength(150)]
        public string KeywordName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PaperKeyword> PaperKeywords { get; set; }
    }
}

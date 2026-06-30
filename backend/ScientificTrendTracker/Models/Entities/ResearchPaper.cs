namespace ScientificTrendTracker.Models.Entities
{
    public class ResearchPaper
    {
        [Key]
        [MaxLength(50)]
        public string PaperId { get; set; }

        [MaxLength(100)]
        public string OpenAlexId { get; set; }

        [MaxLength(255)]
        public string Doi { get; set; }

        [Required]
        [MaxLength(500)]
        public string Title { get; set; }

        public int? PublicationYear { get; set; }
        public DateTime? PublicationDate { get; set; }

        [MaxLength(50)]
        public string JournalId { get; set; }

        public int CitationCount { get; set; } = 0;

        [MaxLength(500)]
        public string SourceUrl { get; set; }

        // Chủ đề chính (primary_topic.display_name) từ OpenAlex — LƯU DB để phân loại/lọc nhanh.
        // Subfield/Field/Domain KHÔNG lưu, lấy on-demand từ OpenAlex ở màn chi tiết.
        [MaxLength(255)]
        public string Topic { get; set; }

        public bool IsAiProcessed { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Journal Journal { get; set; }
        public ICollection<PaperAuthor> PaperAuthors { get; set; }
        public ICollection<PaperKeyword> PaperKeywords { get; set; }

        public ICollection<PaperCitation> CitationsMade { get; set; }
        public ICollection<PaperCitation> CitationsReceived { get; set; }
    }
}

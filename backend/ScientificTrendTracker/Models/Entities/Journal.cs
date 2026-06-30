namespace ScientificTrendTracker.Models.Entities
{
    public class Journal
    {
        [Key]
        [MaxLength(50)]
        public string JournalId { get; set; }

        [MaxLength(100)]
        public string OpenAlexId { get; set; }

        [Required]
        [MaxLength(255)]
        public string JournalName { get; set; }

        [MaxLength(20)]
        public string IssnPrint { get; set; }

        [MaxLength(20)]
        public string IssnElectronic { get; set; }

        [MaxLength(255)]
        public string Publisher { get; set; }

        [MaxLength(150)]
        public string FieldOfStudy { get; set; }

        [MaxLength(100)]
        public string IndexingDatabase { get; set; }

        [MaxLength(10)]
        public string QuartileRank { get; set; }

        public int? RankingYear { get; set; }

        [Column(TypeName = "decimal(6,3)")]
        public decimal? ImpactFactor { get; set; }

        public int? HIndex { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ResearchPaper> ResearchPapers { get; set; }
    }
}

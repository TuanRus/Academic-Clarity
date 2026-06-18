namespace ScientificTrendTracker.Models.Entities
{
    public class PaperCitation
    {
        [MaxLength(50)]
        public string CitingPaperId { get; set; }
        [MaxLength(50)]
        public string CitedPaperId { get; set; }

        public ResearchPaper CitingPaper { get; set; }
        public ResearchPaper CitedPaper { get; set; }
    }
}

namespace ScientificTrendTracker.Models.Entities
{
    public class PaperTopic
    {
        [MaxLength(50)]
        public string PaperId { get; set; }
        [MaxLength(50)]
        public string TopicId { get; set; }

        public decimal? ConfidenceScore { get; set; }

        public ResearchPaper Paper { get; set; }
        public ResearchTopic Topic { get; set; }
    }
}

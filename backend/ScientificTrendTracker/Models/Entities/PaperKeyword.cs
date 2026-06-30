namespace ScientificTrendTracker.Models.Entities
{
    public class PaperKeyword
    {
        [MaxLength(50)]
        public string PaperId { get; set; }
        [MaxLength(50)]
        public string KeywordId { get; set; }

        public ResearchPaper Paper { get; set; }
        public Keyword Keyword { get; set; }
    }
}

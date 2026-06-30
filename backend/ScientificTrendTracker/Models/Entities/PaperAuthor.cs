namespace ScientificTrendTracker.Models.Entities
{
    public class PaperAuthor
    {
        [MaxLength(50)]
        public string PaperId { get; set; }
        public int AuthorId { get; set; }
        public int AuthorOrder { get; set; }

        public ResearchPaper Paper { get; set; }
        public Author Author { get; set; }
    }
}

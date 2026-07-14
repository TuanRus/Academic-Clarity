namespace ScientificTrendTracker.Models.DTOs
{
    /// <summary>
    /// Chi tiết đầy đủ 1 bài báo cho màn Paper Detail của FE.
    /// Thông tin cốt lõi lấy từ DB; Abstract reconstruct on-demand từ OpenAlex (không lưu DB).
    /// </summary>
    public class PaperDetailDto
    {
        public string PaperId { get; set; }
        public string OpenAlexId { get; set; }
        public string Doi { get; set; }
        public string Title { get; set; }
        public int? PublicationYear { get; set; }
        public string PublicationDate { get; set; }
        public int CitationCount { get; set; }
        public string SourceUrl { get; set; }

        // Journal (nếu có)
        public string JournalName { get; set; }
        public string Quartile { get; set; }
        public string Publisher { get; set; }
        public decimal? ImpactFactor { get; set; }

        public List<string> Authors { get; set; } = new();
        public List<string> Keywords { get; set; } = new();

        /// <summary>Chủ đề chính — lưu DB (primary_topic.display_name từ OpenAlex lúc sync).</summary>
        public string Topic { get; set; }

        // Lấy on-demand từ OpenAlex (không lưu DB). Null nếu OpenAlex không có.
        public string Subfield { get; set; }
        public string Field { get; set; }
        public string Domain { get; set; }
        public string OpenAccessStatus { get; set; }
        public List<string> Institutions { get; set; } = new();

        /// <summary>Abstract ghép từ OpenAlex inverted index; null nếu OpenAlex không có.</summary>
        public string Abstract { get; set; }
    }
}

namespace ScientificTrendTracker.Models.DTOs
{
    /// <summary>Request: đoạn abstract người dùng dán lên (xử lý in-memory, KHÔNG lưu DB).</summary>
    public class OverlapCheckRequest
    {
        public string Abstract { get; set; }
    }

    /// <summary>1 bài báo trùng keyword với abstract đầu vào.</summary>
    public class OverlapMatchDto
    {
        public string PaperId { get; set; }
        public string Title { get; set; }
        public int? Year { get; set; }
        public int CitationCount { get; set; }
        public string JournalName { get; set; }
        public string SourceUrl { get; set; }
        /// <summary>Các keyword mà bài này chia sẻ với abstract đầu vào.</summary>
        public List<string> SharedKeywords { get; set; } = new();
        /// <summary>Điểm trùng (0..1) theo weighted overlap (keyword hiếm nặng hơn).</summary>
        public double Score { get; set; }
        /// <summary>Mức cảnh báo: "high" (≥0.30) / "medium" (0.15–0.30) / "low".</summary>
        public string Tier { get; set; }
    }

    /// <summary>
    /// Kết quả CẢNH BÁO SỚM mức độ trùng lặp keyword (KHÔNG phải phát hiện đạo văn).
    /// </summary>
    public class OverlapResultDto
    {
        /// <summary>Keyword AI trích từ abstract đầu vào.</summary>
        public List<string> ExtractedKeywords { get; set; } = new();
        /// <summary>Số keyword (trong số trên) thực sự tồn tại trong database.</summary>
        public int MatchedKeywordCount { get; set; }
        /// <summary>Danh sách bài trùng nhiều keyword nhất, xếp theo điểm giảm dần.</summary>
        public List<OverlapMatchDto> Matches { get; set; } = new();
    }
}

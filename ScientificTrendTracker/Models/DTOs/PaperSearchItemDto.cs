namespace ScientificTrendTracker.Models.DTOs
{
    /// <summary>
    /// Một dòng kết quả tìm kiếm bài báo (bước "search → hiện danh sách").
    /// Đủ thông tin để FE hiển thị list cho người dùng chọn, chưa phải graph.
    /// </summary>
    public class PaperSearchItemDto
    {
        /// <summary>PaperId — dùng để gọi tiếp /api/mindmap/graph/paper/{paperId} khi user click.</summary>
        public string PaperId { get; set; }

        /// <summary>Tiêu đề đầy đủ của bài báo.</summary>
        public string Title { get; set; }

        /// <summary>Năm xuất bản.</summary>
        public int? Year { get; set; }

        /// <summary>Số lượt trích dẫn.</summary>
        public int CitationCount { get; set; }

        /// <summary>Tên tạp chí (có thể null nếu không xác định nguồn).</summary>
        public string JournalName { get; set; }

        /// <summary>Xếp hạng Q của tạp chí (Q1-Q4) từ SCImago, có thể null.</summary>
        public string Quartile { get; set; }

        /// <summary>Link tới bài báo gốc trên OpenAlex.</summary>
        public string SourceUrl { get; set; }

        /// <summary>Số keyword đã extract — 0 nghĩa là click vào sẽ ra graph trơ (chưa có keyword).</summary>
        public int KeywordCount { get; set; }
    }
}

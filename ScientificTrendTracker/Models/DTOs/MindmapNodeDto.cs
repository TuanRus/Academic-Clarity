namespace ScientificTrendTracker.Models.DTOs
{
    /// <summary>
    /// Đại diện cho một node trong mind map graph.
    /// Có 2 loại node: "keyword" (hub trung tâm) và "paper" (leaf bài báo).
    /// FE dùng trường Type để render màu sắc và kích thước node khác nhau.
    /// </summary>
    public class MindmapNodeDto
    {
        /// <summary>
        /// ID duy nhất của node trong graph.
        /// - Keyword node: "kw_{KeywordId}"
        /// - Paper node: "p_{PaperId}"
        /// FE dùng làm key để render và tạo edge.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Loại node: "keyword" hoặc "paper".
        /// FE dùng để phân biệt màu sắc, hình dạng, kích thước node.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Nhãn hiển thị trên node.
        /// - Keyword node: tên keyword (vd: "machine-learning")
        /// - Paper node: tiêu đề bài báo (rút gọn nếu quá dài)
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Năm xuất bản. Chỉ có giá trị với Paper node, null với Keyword node.
        /// FE dùng để tô màu node theo thời gian (node cũ màu nhạt, node mới màu đậm).
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// Số lượt trích dẫn. Chỉ có giá trị với Paper node, null với Keyword node.
        /// FE dùng để điều chỉnh kích thước node (nhiều citation = node to hơn).
        /// </summary>
        public int? CitationCount { get; set; }

        /// <summary>
        /// Xếp hạng Q của journal (Q1/Q2/Q3/Q4). Chỉ có với Paper node.
        /// FE dùng để hiển thị badge hoặc viền màu trên node.
        /// </summary>
        public string Quartile { get; set; }

        /// <summary>
        /// Số bài báo liên kết với keyword này. Chỉ có giá trị với Keyword node.
        /// FE dùng để điều chỉnh kích thước của keyword hub (nhiều paper = hub to hơn).
        /// </summary>
        public int? PaperCount { get; set; }

        /// <summary>
        /// Điểm xu hướng từ 0.0 đến 1.0, chỉ có với Keyword node.
        /// Tính dựa trên tỷ lệ bài báo gần đây (2023-nay) / tổng bài báo của keyword.
        /// FE dùng để hiển thị icon "đang hot" khi TrendScore > 0.6.
        /// </summary>
        public double? TrendScore { get; set; }

        /// <summary>
        /// URL đến bài báo gốc (DOI hoặc OpenAlex URL). Chỉ có với Paper node.
        /// FE dùng để tạo link click-through ra trang bài báo.
        /// </summary>
        public string SourceUrl { get; set; }

        /// <summary>
        /// Tầng trong mind map cây: 0 = chủ đề trung tâm, 1 = chủ đề con (nhánh), 2 = bài báo (lá).
        /// Chỉ điền khi dùng endpoint tree. FE dùng để layout phân tầng.
        /// </summary>
        public int? Level { get; set; }
    }
}

namespace ScientificTrendTracker.Models.DTOs
{
    /// <summary>
    /// Đại diện cho một cạnh (edge) kết nối Paper node với Keyword node trong mind map.
    /// Hướng luôn là Paper → Keyword (bài báo này dùng keyword này).
    /// FE dùng Source và Target để vẽ đường nối giữa các node.
    /// </summary>
    public class MindmapEdgeDto
    {
        /// <summary>
        /// ID của node nguồn (Paper node), dạng "p_{PaperId}".
        /// Khớp với MindmapNodeDto.Id của Paper node tương ứng.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// ID của node đích (Keyword node), dạng "kw_{KeywordId}".
        /// Khớp với MindmapNodeDto.Id của Keyword node tương ứng.
        /// </summary>
        public string Target { get; set; }
    }
}

namespace ScientificTrendTracker.Models.DTOs
{
    /// <summary>
    /// JSON object chính trả về cho FE để render mind map.
    /// Chứa toàn bộ nodes và edges của subgraph xoay quanh keyword hoặc paper được search.
    /// FE nhận object này và đưa thẳng vào thư viện graph (D3.js, Cytoscape, v.v.).
    /// </summary>
    public class MindmapGraphDto
    {
        /// <summary>
        /// Từ khóa hoặc tiêu đề người dùng đã search, dùng để hiển thị tiêu đề graph trên FE.
        /// </summary>
        public string SearchQuery { get; set; }

        /// <summary>
        /// Tổng số node trong graph (bao gồm cả keyword node và paper node).
        /// FE dùng để hiển thị thống kê "X nodes found".
        /// </summary>
        public int TotalNodes { get; set; }

        /// <summary>
        /// Tổng số edge trong graph.
        /// FE dùng để hiển thị thống kê "X connections".
        /// </summary>
        public int TotalEdges { get; set; }

        /// <summary>
        /// Danh sách tất cả nodes trong graph.
        /// Mỗi node có Type = "keyword" hoặc "paper".
        /// FE iterate qua list này để render từng node.
        /// </summary>
        public List<MindmapNodeDto> Nodes { get; set; } = new();

        /// <summary>
        /// Danh sách tất cả edges kết nối Paper node với Keyword node.
        /// FE iterate qua list này để vẽ đường nối giữa các node.
        /// </summary>
        public List<MindmapEdgeDto> Edges { get; set; } = new();
    }
}

using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs;

namespace ScientificTrendTracker.Services.Interfaces
{
    public interface IGraphBuilderService
    {
        /// <summary>
        /// Bước "search → hiện danh sách": tìm bài báo theo tiêu đề/DOI gần đúng, trả về list phân trang.
        /// User chọn 1 bài rồi gọi BuildGraphByPaperIdAsync để xem graph.
        /// </summary>
        /// <param name="query">
        /// string - FE truyền lên - Từ khóa tìm trong Title hoặc Doi (contains, không phân biệt hoa thường).
        /// </param>
        /// <param name="page">
        /// int - FE truyền lên - Trang hiện tại, bắt đầu từ 1.
        /// </param>
        /// <param name="pageSize">
        /// int - FE truyền lên - Số bài mỗi trang.
        /// </param>
        /// <returns>
        /// PagedResult&lt;PaperSearchItemDto&gt; - Danh sách bài báo khớp (sắp theo citation giảm dần) kèm tổng số.
        /// </returns>
        Task<PagedResult<PaperSearchItemDto>> SearchPapersAsync(string query, int page, int pageSize);

        /// <summary>Tìm bài báo theo TÊN TÁC GIẢ (contains). Sắp theo citation giảm dần, phân trang.</summary>
        Task<PagedResult<PaperSearchItemDto>> SearchPapersByAuthorAsync(string author, int page, int pageSize);

        /// <summary>Tìm bài báo theo TÊN TẠP CHÍ (contains). Sắp theo citation giảm dần, phân trang.</summary>
        Task<PagedResult<PaperSearchItemDto>> SearchPapersByJournalAsync(string journal, int page, int pageSize);

        /// <summary>
        /// Gợi ý keyword CÓ SẴN trong DB khớp chuỗi gõ vào (autocomplete cho thanh search).
        /// </summary>
        /// <param name="q">Chuỗi người dùng đang gõ (contains, không phân biệt hoa thường).</param>
        /// <param name="limit">Số gợi ý tối đa.</param>
        /// <returns>List&lt;string&gt; tên keyword, ưu tiên keyword nhiều bài nhất.</returns>
        Task<List<string>> SuggestKeywordsAsync(string q, int limit);

        /// <summary>
        /// Bước "click vào bài báo → tạo graph": dựng graph cho đúng 1 bài báo theo PaperId.
        /// Khác BuildGraphByPaperAsync (nhận query fuzzy) — đây nhận PaperId chính xác user đã chọn.
        /// </summary>
        /// <param name="paperId">
        /// string - PaperId lấy từ kết quả SearchPapersAsync.
        /// </param>
        /// <param name="maxSiblings">
        /// int - Số sibling paper tối đa mỗi keyword, mặc định 5.
        /// </param>
        /// <returns>
        /// MindmapGraphDto - Graph xoay quanh bài báo đó. Nodes/Edges rỗng nếu PaperId không tồn tại.
        /// </returns>
        Task<MindmapGraphDto> BuildGraphByPaperIdAsync(string paperId, int maxSiblings = 5);

        /// <summary>
        /// Dựng mind map dạng CÂY 3 tầng KEYWORD (kiểu sơ đồ tư duy):
        /// Tầng 0 = chủ đề trung tâm → Tầng 1 = chủ đề con (keyword đồng xuất hiện với gốc)
        /// → Tầng 2 = chủ đề cháu (keyword đồng xuất hiện với từng chủ đề con).
        /// Bài báo KHÔNG nằm trên cây — FE gọi GetTopPapersByKeyword khi click 1 node.
        /// </summary>
        /// <param name="keyword">string - Chủ đề trung tâm, ưu tiên match chính xác.</param>
        /// <param name="maxBranches">int - Số chủ đề con (tầng 1) tối đa, mặc định 6.</param>
        /// <param name="maxSubBranches">int - Số chủ đề cháu (tầng 2) mỗi nhánh, mặc định 3.</param>
        /// <returns>
        /// MindmapGraphDto - Nodes toàn keyword (Level 0/1/2), Edges nối cha→con. Rỗng nếu không thấy keyword.
        /// </returns>
        Task<MindmapGraphDto> BuildKeywordTreeAsync(string keyword, int maxBranches = 6, int maxSubBranches = 3);

        /// <summary>
        /// Lấy top bài báo của 1 keyword cho panel bên phải khi user click 1 node.
        /// Khi truyền distinctFrom (keyword root đang xem), bài KHÔNG chứa root được ưu tiên lên trước
        /// (rồi mới tới bài chứa cả root), để sub-node hiện bài ĐẶC TRƯNG thay vì lặp lại megahit của root.
        /// </summary>
        /// <param name="keyword">Keyword của node được click (exact trước, fallback contains).</param>
        /// <param name="distinctFrom">Keyword root đang xem; null khi click chính node root.</param>
        /// <param name="limit">Số bài tối đa, mặc định 10.</param>
        Task<List<PaperSearchItemDto>> GetTopPapersByKeywordAsync(string keyword, string distinctFrom = null, int limit = 10);
    }
}

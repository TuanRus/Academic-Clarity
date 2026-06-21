using Microsoft.AspNetCore.Mvc;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Controllers
{
    [ApiController]
    [Route("api/mindmap")]
    public class MindmapController : ControllerBase
    {
        private readonly IGraphBuilderService _graphBuilderService;
        private readonly ILogger<MindmapController> _logger;

        public MindmapController(IGraphBuilderService graphBuilderService, ILogger<MindmapController> logger)
        {
            _graphBuilderService = graphBuilderService;
            _logger = logger;
        }

        /// <summary>
        /// Mind map dạng CÂY 3 tầng KEYWORD (sơ đồ tư duy): chủ đề trung tâm → chủ đề con → chủ đề cháu.
        /// Bài báo KHÔNG nằm trên cây — FE gọi /papers/keyword khi user click 1 node để xổ danh sách bài.
        /// </summary>
        /// <param name="keyword">
        /// string - FE truyền lên qua query string (?keyword=...) - Chủ đề trung tâm, ưu tiên khớp chính xác.
        /// </param>
        /// <param name="maxBranches">
        /// int - FE truyền lên qua query string (?maxBranches=6) - Số chủ đề con (tầng 1), mặc định 6.
        /// </param>
        /// <param name="maxSubBranches">
        /// int - FE truyền lên qua query string (?maxSubBranches=3) - Số chủ đề cháu mỗi nhánh (tầng 2), mặc định 3.
        /// </param>
        /// <returns>
        /// Chuỗi JSON (ApiResponse&lt;MindmapGraphDto&gt;) gửi cho FE render mind map. Thuộc tính Data bao gồm:
        /// - SearchQuery (string): Keyword đã search.
        /// - TotalNodes (int): Tổng số node trong cây.
        /// - TotalEdges (int): Tổng số cạnh (cha→con).
        /// - Nodes (Array): Toàn node keyword, mỗi node gồm Id, Type ("keyword"), Label,
        ///   Level (0=tâm, 1=chủ đề con, 2=chủ đề cháu), PaperCount, TrendScore.
        /// - Edges (Array): Mỗi cạnh gồm Source, Target (Id node).
        /// Trả 400 nếu keyword rỗng/tham số sai, 404 nếu không tìm thấy keyword.
        /// </returns>
        [HttpGet("tree/keyword")]
        public async Task<IActionResult> TreeByKeyword(
            [FromQuery] string keyword,
            [FromQuery] int maxBranches = 6,
            [FromQuery] int maxSubBranches = 3)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return BadRequest(ApiResponse<object>.Fail(400, "Keyword không được để trống."));

            if (maxBranches < 1 || maxBranches > 15)
                return BadRequest(ApiResponse<object>.Fail(400, "maxBranches phải từ 1 đến 15."));
            if (maxSubBranches < 0 || maxSubBranches > 10)
                return BadRequest(ApiResponse<object>.Fail(400, "maxSubBranches phải từ 0 đến 10."));

            var graph = await _graphBuilderService.BuildKeywordTreeAsync(keyword.Trim(), maxBranches, maxSubBranches);

            if (graph.TotalNodes == 0)
                return NotFound(ApiResponse<object>.Fail(404, $"Không tìm thấy keyword '{keyword}'."));

            return Ok(ApiResponse<MindmapGraphDto>.Ok(graph,
                $"Cây mind map: {graph.TotalNodes} nodes, {graph.TotalEdges} nhánh."));
        }

        /// <summary>
        /// BƯỚC 1 — Search: người dùng gõ từ khóa → trả về DANH SÁCH bài báo khớp (chưa phải graph).
        /// FE hiển thị list cho user chọn, rồi gọi /api/mindmap/graph/paper/{paperId} khi click.
        /// </summary>
        /// <param name="q">
        /// string - FE truyền lên (?q=...) - Từ khóa tìm trong tiêu đề/DOI, tìm gần đúng.
        /// </param>
        /// <param name="page">
        /// int - FE truyền lên (?page=...) - Trang hiện tại, mặc định 1.
        /// </param>
        /// <param name="pageSize">
        /// int - FE truyền lên (?pageSize=...) - Số bài mỗi trang, mặc định 20.
        /// </param>
        /// <returns>
        /// ApiResponse&lt;PagedResult&lt;PaperSearchItemDto&gt;&gt; - Danh sách bài báo phân trang.
        /// Mỗi item gồm: paperId, title, year, citationCount, journalName, quartile, sourceUrl, keywordCount.
        /// </returns>
        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery] string q,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(ApiResponse<object>.Fail(400, "Từ khóa tìm kiếm không được để trống."));

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var result = await _graphBuilderService.SearchPapersAsync(q.Trim(), page, pageSize);

            return Ok(ApiResponse<PagedResult<PaperSearchItemDto>>.Ok(result,
                $"Tìm thấy {result.TotalCount} bài báo cho '{q}'."));
        }

        /// <summary>
        /// Autocomplete: gợi ý keyword CÓ SẴN trong DB khớp chuỗi gõ vào (cho thanh search).
        /// </summary>
        /// <param name="q">string - FE truyền qua query (?q=) - Chuỗi đang gõ.</param>
        /// <param name="limit">int - FE truyền qua query (?limit=10) - Số gợi ý tối đa, mặc định 10.</param>
        /// <returns>
        /// Chuỗi JSON (ApiResponse&lt;List&lt;string&gt;&gt;) - Data là mảng tên keyword khớp, sắp theo số bài giảm dần.
        /// Rỗng nếu q trống hoặc không khớp.
        /// </returns>
        [HttpGet("keywords/suggest")]
        public async Task<IActionResult> SuggestKeywords([FromQuery] string q, [FromQuery] int limit = 10)
        {
            if (limit < 1 || limit > 25) limit = 10;
            var items = await _graphBuilderService.SuggestKeywordsAsync(q, limit);
            return Ok(ApiResponse<List<string>>.Ok(items, $"{items.Count} gợi ý."));
        }

        /// <summary>
        /// Search theo TÊN TÁC GIẢ → trả về DANH SÁCH bài báo của tác giả đó (sắp theo citation giảm dần).
        /// Click 1 bài rồi gọi /api/mindmap/graph/paper/{paperId} để xem mindmap.
        /// </summary>
        /// <param name="author">
        /// string - FE truyền lên qua query string (?author=...) - Tên tác giả, tìm gần đúng (contains).
        /// </param>
        /// <param name="page">
        /// int - FE truyền lên qua query string (?page=1) - Trang hiện tại, mặc định 1.
        /// </param>
        /// <param name="pageSize">
        /// int - FE truyền lên qua query string (?pageSize=20) - Số bài mỗi trang, mặc định 20.
        /// </param>
        /// <returns>
        /// Chuỗi JSON (ApiResponse&lt;PagedResult&lt;PaperSearchItemDto&gt;&gt;) gửi cho FE. Thuộc tính Data bao gồm:
        /// - Items (Array): Danh sách bài, mỗi item gồm PaperId, Title, Year, CitationCount, JournalName, Quartile, SourceUrl, KeywordCount.
        /// - TotalCount (int): Tổng số bài khớp.
        /// - Page (int), PageSize (int): Thông tin phân trang.
        /// </returns>
        [HttpGet("search/author")]
        public async Task<IActionResult> SearchByAuthor(
            [FromQuery] string author,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(author))
                return BadRequest(ApiResponse<object>.Fail(400, "Tên tác giả không được để trống."));

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var result = await _graphBuilderService.SearchPapersByAuthorAsync(author.Trim(), page, pageSize);

            return Ok(ApiResponse<PagedResult<PaperSearchItemDto>>.Ok(result,
                $"Tìm thấy {result.TotalCount} bài báo của tác giả '{author}'."));
        }

        /// <summary>
        /// Search theo TÊN TẠP CHÍ → trả về DANH SÁCH bài báo đăng trong tạp chí đó (sắp theo citation giảm dần).
        /// Click 1 bài rồi gọi /api/mindmap/graph/paper/{paperId} để xem mindmap.
        /// </summary>
        /// <param name="journal">
        /// string - FE truyền lên qua query string (?journal=...) - Tên tạp chí, tìm gần đúng (contains).
        /// </param>
        /// <param name="page">
        /// int - FE truyền lên qua query string (?page=1) - Trang hiện tại, mặc định 1.
        /// </param>
        /// <param name="pageSize">
        /// int - FE truyền lên qua query string (?pageSize=20) - Số bài mỗi trang, mặc định 20.
        /// </param>
        /// <returns>
        /// Chuỗi JSON (ApiResponse&lt;PagedResult&lt;PaperSearchItemDto&gt;&gt;) gửi cho FE. Thuộc tính Data bao gồm:
        /// - Items (Array): Danh sách bài, mỗi item gồm PaperId, Title, Year, CitationCount, JournalName, Quartile, SourceUrl, KeywordCount.
        /// - TotalCount (int): Tổng số bài khớp.
        /// - Page (int), PageSize (int): Thông tin phân trang.
        /// </returns>
        [HttpGet("search/journal")]
        public async Task<IActionResult> SearchByJournal(
            [FromQuery] string journal,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(journal))
                return BadRequest(ApiResponse<object>.Fail(400, "Tên tạp chí không được để trống."));

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var result = await _graphBuilderService.SearchPapersByJournalAsync(journal.Trim(), page, pageSize);

            return Ok(ApiResponse<PagedResult<PaperSearchItemDto>>.Ok(result,
                $"Tìm thấy {result.TotalCount} bài báo trong tạp chí '{journal}'."));
        }

        /// <summary>
        /// BƯỚC 2 — Click: user chọn 1 bài từ danh sách → dựng mind map graph cho bài đó theo PaperId.
        /// </summary>
        /// <param name="paperId">
        /// string - FE truyền qua route param (/graph/paper/{paperId}) - PaperId lấy từ kết quả các endpoint search.
        /// </param>
        /// <param name="maxSiblings">
        /// int - FE truyền lên qua query string (?maxSiblings=5) - Số bài cùng keyword (sibling) mỗi keyword, mặc định 5.
        /// </param>
        /// <returns>
        /// Chuỗi JSON (ApiResponse&lt;MindmapGraphDto&gt;) gửi cho FE render mind map. Thuộc tính Data bao gồm:
        /// - SearchQuery (string): PaperId đã dựng graph.
        /// - TotalNodes (int): Tổng số node (bài gốc + keyword + sibling).
        /// - TotalEdges (int): Tổng số cạnh (bài → keyword).
        /// - Nodes (Array): Bài gốc + các keyword node + sibling paper, mỗi node gồm Id, Type, Label,
        ///   và với node bài: Year, CitationCount, Quartile, SourceUrl; với node keyword: PaperCount, TrendScore.
        /// - Edges (Array): Mỗi cạnh gồm Source, Target.
        /// Trả 400 nếu PaperId rỗng, 404 nếu PaperId không tồn tại.
        /// </returns>
        [HttpGet("graph/paper/{paperId}")]
        public async Task<IActionResult> GraphByPaperId(
            string paperId,
            [FromQuery] int maxSiblings = 5)
        {
            if (string.IsNullOrWhiteSpace(paperId))
                return BadRequest(ApiResponse<object>.Fail(400, "PaperId không được để trống."));

            if (maxSiblings < 1 || maxSiblings > 20)
                return BadRequest(ApiResponse<object>.Fail(400, "maxSiblings phải từ 1 đến 20."));

            var graph = await _graphBuilderService.BuildGraphByPaperIdAsync(paperId, maxSiblings);

            if (graph.TotalNodes == 0)
                return NotFound(ApiResponse<object>.Fail(404, $"Không tìm thấy bài báo với PaperId '{paperId}'."));

            return Ok(ApiResponse<MindmapGraphDto>.Ok(graph,
                $"Graph với {graph.TotalNodes} nodes và {graph.TotalEdges} connections."));
        }

        /// <summary>
        /// Panel bên phải: top bài báo của 1 keyword khi user click vào node trong mind map.
        /// Truyền distinctFrom = keyword root đang xem để sub-node hiện bài ĐẶC TRƯNG (không lặp megahit của root).
        /// Click chính node root thì bỏ distinctFrom.
        /// </summary>
        /// <param name="keyword">
        /// string - FE truyền lên qua query string (?keyword=...) - Keyword của node user vừa click.
        /// </param>
        /// <param name="distinctFrom">
        /// string - FE truyền lên qua query string (?distinctFrom=...) - Keyword root đang xem (tùy chọn, null khi click node root).
        /// Khi có giá trị, bài KHÔNG chứa root được ưu tiên lên trước để sub-node hiện bài đặc trưng.
        /// </param>
        /// <param name="limit">
        /// int - FE truyền lên qua query string (?limit=10) - Số bài tối đa, mặc định 10.
        /// </param>
        /// <returns>
        /// Chuỗi JSON (ApiResponse&lt;List&lt;PaperSearchItemDto&gt;&gt;) gửi cho FE render panel bên phải.
        /// Thuộc tính Data là mảng bài báo, mỗi item gồm:
        /// - PaperId (string), Title (string), Year (int?), CitationCount (int)
        /// - JournalName (string), Quartile (string), SourceUrl (string), KeywordCount (int)
        /// Trả 400 nếu keyword rỗng, 404 nếu không có bài nào cho keyword.
        /// </returns>
        [HttpGet("papers/keyword")]
        public async Task<IActionResult> GetTopPapersByKeyword(
            [FromQuery] string keyword,
            [FromQuery] string distinctFrom = null,
            [FromQuery] int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return BadRequest(ApiResponse<object>.Fail(400, "keyword không được để trống."));

            if (limit < 1 || limit > 50) limit = 10;

            var papers = await _graphBuilderService.GetTopPapersByKeywordAsync(keyword.Trim(), distinctFrom, limit);

            if (papers.Count == 0)
                return NotFound(ApiResponse<object>.Fail(404,
                    $"Không tìm thấy bài báo nào cho keyword '{keyword}'."));

            return Ok(ApiResponse<List<PaperSearchItemDto>>.Ok(papers,
                $"Top {papers.Count} bài cho '{keyword}'" + (string.IsNullOrWhiteSpace(distinctFrom) ? "." : $" (đặc trưng so với '{distinctFrom}').")));
        }
    }
}

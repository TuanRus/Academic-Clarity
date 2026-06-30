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

        /// <summary>Mind map dạng cây 3 tầng quanh 1 keyword (chủ đề → con → cháu). Bài báo không nằm trên cây.</summary>
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

        /// <summary>Tìm bài báo theo từ khóa (tiêu đề / DOI / keyword), phân trang.</summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery] string q,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int? fromYear = null,
            [FromQuery] int? toYear = null)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(ApiResponse<object>.Fail(400, "Từ khóa tìm kiếm không được để trống."));

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var result = await _graphBuilderService.SearchPapersAsync(q.Trim(), page, pageSize, fromYear, toYear);

            return Ok(ApiResponse<PagedResult<PaperSearchItemDto>>.Ok(result,
                $"Tìm thấy {result.TotalCount} bài báo cho '{q}'."));
        }

        /// <summary>Autocomplete: gợi ý keyword có sẵn trong DB, sắp theo số bài giảm dần.</summary>
        [HttpGet("keywords/suggest")]
        public async Task<IActionResult> SuggestKeywords([FromQuery] string q, [FromQuery] int limit = 10)
        {
            if (limit < 1 || limit > 25) limit = 10;
            var items = await _graphBuilderService.SuggestKeywordsAsync(q, limit);
            return Ok(ApiResponse<List<string>>.Ok(items, $"{items.Count} gợi ý."));
        }

        /// <summary>Tìm bài báo theo tên tác giả (gần đúng), phân trang.</summary>
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

        /// <summary>Tìm bài báo theo tên tạp chí (gần đúng), phân trang.</summary>
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

        /// <summary>Chi tiết 1 bài báo cho màn Paper Detail (DB + abstract/topic/OA lấy on-demand từ OpenAlex).</summary>
        [HttpGet("paper/{paperId}")]
        public async Task<IActionResult> GetPaperDetail(string paperId)
        {
            if (string.IsNullOrWhiteSpace(paperId))
                return BadRequest(ApiResponse<object>.Fail(400, "PaperId không được để trống."));

            var detail = await _graphBuilderService.GetPaperDetailAsync(paperId);

            if (detail == null)
                return NotFound(ApiResponse<object>.Fail(404, $"Không tìm thấy bài báo với PaperId '{paperId}'."));

            return Ok(ApiResponse<PaperDetailDto>.Ok(detail, $"Chi tiết bài '{detail.Title}'."));
        }

        /// <summary>Top bài báo của 1 keyword (panel chi tiết khi click node). distinctFrom để bỏ trùng megahit của chủ đề gốc.</summary>
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

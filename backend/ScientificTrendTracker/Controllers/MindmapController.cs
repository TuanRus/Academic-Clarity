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
                return BadRequest(ApiResponse<object>.Fail(400, "Keyword cannot be empty."));

            if (maxBranches < 1 || maxBranches > 15)
                return BadRequest(ApiResponse<object>.Fail(400, "maxBranches must be between 1 and 15."));
            if (maxSubBranches < 0 || maxSubBranches > 10)
                return BadRequest(ApiResponse<object>.Fail(400, "maxSubBranches must be between 0 and 10."));

            var graph = await _graphBuilderService.BuildKeywordTreeAsync(keyword.Trim(), maxBranches, maxSubBranches);

            if (graph.TotalNodes == 0)
                return NotFound(ApiResponse<object>.Fail(404, $"Could not find keyword '{keyword}'."));

            return Ok(ApiResponse<MindmapGraphDto>.Ok(graph,
                $"Mind map tree: {graph.TotalNodes} nodes, {graph.TotalEdges} branch(es)."));
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
                return BadRequest(ApiResponse<object>.Fail(400, "Search query cannot be empty."));

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var result = await _graphBuilderService.SearchPapersAsync(q.Trim(), page, pageSize, fromYear, toYear);

            return Ok(ApiResponse<PagedResult<PaperSearchItemDto>>.Ok(result,
                $"Found {result.TotalCount} paper(s) for '{q}'."));
        }

        /// <summary>Autocomplete: gợi ý keyword có sẵn trong DB, sắp theo số bài giảm dần.</summary>
        [HttpGet("keywords/suggest")]
        public async Task<IActionResult> SuggestKeywords([FromQuery] string q, [FromQuery] int limit = 10)
        {
            if (limit < 1 || limit > 25) limit = 10;
            var items = await _graphBuilderService.SuggestKeywordsAsync(q, limit);
            return Ok(ApiResponse<List<string>>.Ok(items, $"{items.Count} suggestion(s)."));
        }

        /// <summary>Tìm bài báo theo tên tác giả (gần đúng), phân trang.</summary>
        [HttpGet("search/author")]
        public async Task<IActionResult> SearchByAuthor(
            [FromQuery] string author,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(author))
                return BadRequest(ApiResponse<object>.Fail(400, "Author name cannot be empty."));

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var result = await _graphBuilderService.SearchPapersByAuthorAsync(author.Trim(), page, pageSize);

            return Ok(ApiResponse<PagedResult<PaperSearchItemDto>>.Ok(result,
                $"Found {result.TotalCount} paper(s) by author '{author}'."));
        }

        /// <summary>Tìm bài báo theo tên tạp chí (gần đúng), phân trang.</summary>
        [HttpGet("search/journal")]
        public async Task<IActionResult> SearchByJournal(
            [FromQuery] string journal,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (string.IsNullOrWhiteSpace(journal))
                return BadRequest(ApiResponse<object>.Fail(400, "Journal name cannot be empty."));

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var result = await _graphBuilderService.SearchPapersByJournalAsync(journal.Trim(), page, pageSize);

            return Ok(ApiResponse<PagedResult<PaperSearchItemDto>>.Ok(result,
                $"Found {result.TotalCount} paper(s) in journal '{journal}'."));
        }

        /// <summary>Chi tiết 1 bài báo cho màn Paper Detail (DB + abstract/topic/OA lấy on-demand từ OpenAlex).</summary>
        [HttpGet("paper/{paperId}")]
        public async Task<IActionResult> GetPaperDetail(string paperId)
        {
            if (string.IsNullOrWhiteSpace(paperId))
                return BadRequest(ApiResponse<object>.Fail(400, "PaperId cannot be empty."));

            var detail = await _graphBuilderService.GetPaperDetailAsync(paperId);

            if (detail == null)
                return NotFound(ApiResponse<object>.Fail(404, $"Could not find paper with PaperId '{paperId}'."));

            return Ok(ApiResponse<PaperDetailDto>.Ok(detail, $"Details for '{detail.Title}'."));
        }

        /// <summary>Top bài báo của 1 keyword (panel chi tiết khi click node). distinctFrom để bỏ trùng megahit của chủ đề gốc.</summary>
        [HttpGet("papers/keyword")]
        public async Task<IActionResult> GetTopPapersByKeyword(
            [FromQuery] string keyword,
            [FromQuery] string distinctFrom = null,
            [FromQuery] int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return BadRequest(ApiResponse<object>.Fail(400, "keyword cannot be empty."));

            if (limit < 1 || limit > 50) limit = 10;

            var papers = await _graphBuilderService.GetTopPapersByKeywordAsync(keyword.Trim(), distinctFrom, limit);

            if (papers.Count == 0)
                return NotFound(ApiResponse<object>.Fail(404,
                    $"No papers found for keyword '{keyword}'."));

            return Ok(ApiResponse<List<PaperSearchItemDto>>.Ok(papers,
                $"Top {papers.Count} paper(s) for '{keyword}'" + (string.IsNullOrWhiteSpace(distinctFrom) ? "." : $" (distinct from '{distinctFrom}').")));
        }
    }
}

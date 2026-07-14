using Microsoft.AspNetCore.Mvc;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Controllers
{
    /// <summary>
    /// Idea Overlap Checker (premium) — CẢNH BÁO SỚM mức độ trùng keyword.
    /// KHÔNG phải công cụ phát hiện đạo văn; chỉ hỗ trợ tác giả tự rà soát trước khi viết.
    /// </summary>
    [ApiController]
    [Route("api/idea")]
    public class IdeaController : ControllerBase
    {
        private readonly IIdeaOverlapService _overlapService;

        public IdeaController(IIdeaOverlapService overlapService)
        {
            _overlapService = overlapService;
        }

        /// <summary>
        /// Dán 1 đoạn abstract → trích keyword → so với corpus → trả về vài bài trùng nhiều keyword nhất
        /// kèm mức cảnh báo. Abstract xử lý IN-MEMORY, KHÔNG lưu DB.
        /// </summary>
        /// <param name="request">Body JSON: { "abstract": "..." }</param>
        [HttpPost("check-overlap")]
        public async Task<IActionResult> CheckOverlap([FromBody] OverlapCheckRequest request)
        {
            var text = request?.Abstract?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return BadRequest(ApiResponse<object>.Fail(400, "Vui lòng dán nội dung abstract."));
            if (text.Length < 80)
                return BadRequest(ApiResponse<object>.Fail(400, "Abstract quá ngắn (tối thiểu 80 ký tự) để phân tích đáng tin."));
            if (text.Length > 6000)
                return BadRequest(ApiResponse<object>.Fail(400, "Abstract quá dài (tối đa 6000 ký tự)."));

            var result = await _overlapService.CheckOverlapAsync(text, topN: 10);

            var msg = result.ExtractedKeywords.Count == 0
                ? "Không trích được keyword từ abstract (kiểm tra AI service)."
                : result.Matches.Count == 0
                    ? "Không tìm thấy bài nào trùng keyword đáng kể — ý tưởng có vẻ mới mẻ."
                    : $"Tìm thấy {result.Matches.Count} bài chia sẻ keyword. Đây là CẢNH BÁO SỚM, không phải kết luận trùng lặp.";

            return Ok(ApiResponse<OverlapResultDto>.Ok(result, msg));
        }
    }
}

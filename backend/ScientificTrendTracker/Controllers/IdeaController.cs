using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
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
    [Authorize]
    public class IdeaController : ControllerBase
    {
        private readonly IIdeaOverlapService _overlapService;
        private readonly IMemoryCache _cache;

        // Giới hạn tần suất: mỗi user chỉ được gọi Idea Check 1 lần / 60 giây (tiết kiệm quota Gemini).
        private static readonly TimeSpan RateLimitWindow = TimeSpan.FromSeconds(60);

        public IdeaController(IIdeaOverlapService overlapService, IMemoryCache cache)
        {
            _overlapService = overlapService;
            _cache = cache;
        }

        /// <summary>
        /// Dán 1 đoạn abstract → trích keyword → so với corpus → trả về vài bài trùng nhiều keyword nhất
        /// kèm mức cảnh báo. Abstract xử lý IN-MEMORY, KHÔNG lưu DB. Giới hạn 1 request/phút cho mỗi user.
        /// </summary>
        /// <param name="request">Body JSON: { "abstract": "..." }</param>
        /// <param name="ct">CancellationToken - ASP.NET Core runtime.</param>
        [HttpPost("check-overlap")]
        public async Task<IActionResult> CheckOverlap([FromBody] OverlapCheckRequest request, CancellationToken ct)
        {
            var text = request?.Abstract?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return BadRequest(ApiResponse<object>.Fail(400, "Vui lòng dán nội dung abstract."));
            if (text.Length < 80)
                return BadRequest(ApiResponse<object>.Fail(400, "Abstract quá ngắn (tối thiểu 80 ký tự) để phân tích đáng tin."));
            if (text.Length > 6000)
                return BadRequest(ApiResponse<object>.Fail(400, "Abstract quá dài (tối đa 6000 ký tự)."));

            // Xác định user + áp rate-limit 1 lần/phút.
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized(ApiResponse<object>.Fail(401, "Danh tính người dùng không hợp lệ."));

            var cacheKey = $"idea-rl:{userId}";
            if (_cache.TryGetValue(cacheKey, out DateTime lastCallAt))
            {
                var remain = (int)Math.Ceiling((RateLimitWindow - (DateTime.UtcNow - lastCallAt)).TotalSeconds);
                if (remain > 0)
                    return StatusCode(429, ApiResponse<object>.Fail(429,
                        $"Bạn thao tác quá nhanh. Vui lòng đợi {remain}s rồi thử lại (giới hạn 1 lần/phút)."));
            }
            // Đặt mốc thời gian gọi (tự hết hạn sau 60s) → chặn lần gọi kế trong cửa sổ.
            _cache.Set(cacheKey, DateTime.UtcNow, RateLimitWindow);

            var result = await _overlapService.CheckOverlapAsync(text, topN: 10, ct);

            var msg = result.ExtractedKeywords.Count == 0
                ? "Không trích được keyword từ abstract (kiểm tra AI service)."
                : result.Matches.Count == 0
                    ? "Không tìm thấy bài nào trùng keyword đáng kể — ý tưởng có vẻ mới mẻ."
                    : $"Tìm thấy {result.Matches.Count} bài chia sẻ keyword. Đây là CẢNH BÁO SỚM, không phải kết luận trùng lặp.";

            return Ok(ApiResponse<OverlapResultDto>.Ok(result, msg));
        }
    }
}

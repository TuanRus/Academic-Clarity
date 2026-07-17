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
    /// LaTeX Writer (premium) — hỗ trợ soạn bài nghiên cứu bằng LaTeX trên FE.
    /// Tài liệu LaTeX lưu PHÍA CLIENT (localStorage), backend KHÔNG lưu gì —
    /// chỉ sinh citation read-only từ corpus + proxy compile PDF qua texlive.net.
    /// </summary>
    [ApiController]
    [Route("api/latex")]
    [Authorize]
    public class LatexController : ControllerBase
    {
        private readonly ILatexCitationService _citationService;
        private readonly ILatexCompileService _compileService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IMemoryCache _cache;

        // Compile đi qua dịch vụ công cộng texlive.net → giới hạn 1 lần/10s mỗi user cho lịch sự.
        private static readonly TimeSpan CompileRateWindow = TimeSpan.FromSeconds(10);
        private const int MaxSourceLength = 200_000;

        public LatexController(
            ILatexCitationService citationService,
            ILatexCompileService compileService,
            ISubscriptionService subscriptionService,
            IMemoryCache cache)
        {
            _citationService = citationService;
            _compileService = compileService;
            _subscriptionService = subscriptionService;
            _cache = cache;
        }

        /// <summary>
        /// Sinh citation (BibTeX + \bibitem) cho 1 paper trong corpus để chèn vào tài liệu LaTeX.
        /// Chỉ tài khoản Premium (hoặc Admin) được dùng.
        /// </summary>
        /// <param name="paperId">string - Route parameter - ID paper trong corpus.</param>
        /// <param name="ct">CancellationToken - ASP.NET Core runtime.</param>
        [HttpGet("citation/{paperId}")]
        public async Task<IActionResult> GetCitation(string paperId, CancellationToken ct)
        {
            var denied = await PremiumGuard.CheckAsync(User, _subscriptionService, ct);
            if (denied != null) return denied;

            var citation = await _citationService.GenerateCitationAsync(paperId, ct);
            if (citation == null)
                return NotFound(ApiResponse<object>.Fail(404, "Không tìm thấy paper trong corpus."));

            return Ok(ApiResponse<CitationDto>.Ok(citation, "Đã sinh citation."));
        }

        /// <summary>
        /// Compile tài liệu LaTeX (đơn file) → PDF, proxy qua texlive.net.
        /// Luôn trả 200 khi compile chạy được: Pdf=base64 nếu thành công, ngược lại Log chứa log lỗi TeX.
        /// Chỉ tài khoản Premium (hoặc Admin); giới hạn 1 lần/10 giây mỗi user.
        /// </summary>
        /// <param name="request">Body JSON: { "content": "\\documentclass..." }</param>
        /// <param name="ct">CancellationToken - ASP.NET Core runtime.</param>
        [HttpPost("compile")]
        public async Task<IActionResult> Compile([FromBody] LatexCompileRequest request, CancellationToken ct)
        {
            var source = request?.Content;
            if (string.IsNullOrWhiteSpace(source))
                return BadRequest(ApiResponse<object>.Fail(400, "Tài liệu trống — không có gì để compile."));
            if (source.Length > MaxSourceLength)
                return BadRequest(ApiResponse<object>.Fail(400, $"Tài liệu quá dài (tối đa {MaxSourceLength:N0} ký tự)."));

            var denied = await PremiumGuard.CheckAsync(User, _subscriptionService, ct);
            if (denied != null) return denied;

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            var cacheKey = $"latex-compile-rl:{userIdClaim?.Value}";
            if (_cache.TryGetValue(cacheKey, out DateTime lastCallAt))
            {
                var remain = (int)Math.Ceiling((CompileRateWindow - (DateTime.UtcNow - lastCallAt)).TotalSeconds);
                if (remain > 0)
                    return StatusCode(429, ApiResponse<object>.Fail(429,
                        $"Compiling too fast — please wait {remain}s (limit: 1 compile per 10 seconds)."));
            }
            _cache.Set(cacheKey, DateTime.UtcNow, CompileRateWindow);

            try
            {
                var result = await _compileService.CompileAsync(source, ct);
                var msg = result.Pdf != null
                    ? "Compile thành công."
                    : "Compile thất bại — xem log TeX để sửa lỗi.";
                return Ok(ApiResponse<LatexCompileResultDto>.Ok(result, msg));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // texlive.net không phản hồi (mất mạng/dịch vụ bận) — báo lỗi rõ thay vì 500 chung chung.
                return StatusCode(502, ApiResponse<object>.Fail(502,
                    "Không kết nối được dịch vụ compile (texlive.net). Kiểm tra mạng rồi thử lại, hoặc Export .tex để compile trên Overleaf."));
            }
        }
    }
}

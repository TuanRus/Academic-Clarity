using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly IScimagoImportService _scimagoImportService;
        private readonly IKeywordExtractionService _keywordExtractionService;
        private readonly ISyncOrchestratorService _syncOrchestrator;
        private readonly IKeywordReprocessService _reprocessService;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IScimagoImportService scimagoImportService,
            IKeywordExtractionService keywordExtractionService,
            ISyncOrchestratorService syncOrchestrator,
            IKeywordReprocessService reprocessService,
            AppDbContext dbContext,
            ILogger<AdminController> logger)
        {
            _scimagoImportService = scimagoImportService;
            _keywordExtractionService = keywordExtractionService;
            _syncOrchestrator = syncOrchestrator;
            _reprocessService = reprocessService;
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Xem trạng thái circuit-breaker của các AI provider: provider nào đang bị tắt do hết quota/rate limit
        /// và còn bao lâu nữa bật lại. Không nhận tham số.
        /// </summary>
        /// <returns>
        /// Chuỗi JSON (ApiResponse&lt;object&gt;) gửi cho FE. Thuộc tính Data bao gồm:
        /// - CooldownCount (int): Số provider đang bị tạm tắt (0 = tất cả sẵn sàng).
        /// - Providers (Array): Danh sách provider đang cooldown, mỗi phần tử gồm:
        ///   + Provider (string): Tên provider (vd "ollama", "cerebras").
        ///   + DisabledUntilUtc (DateTime): Thời điểm UTC provider được bật lại.
        ///   + MinutesRemaining (double): Số phút còn lại tới khi bật lại.
        /// Mảng Providers rỗng nếu mọi provider đều sẵn sàng.
        /// </returns>
        [HttpGet("ai-status")]
        public IActionResult GetAiStatus()
        {
            var now = DateTime.UtcNow;
            var cooldowns = _keywordExtractionService.GetProviderCooldowns()
                .Select(kv => new
                {
                    Provider = kv.Key,
                    DisabledUntilUtc = kv.Value,
                    MinutesRemaining = Math.Round((kv.Value - now).TotalMinutes, 1)
                })
                .OrderBy(x => x.DisabledUntilUtc)
                .ToList();

            return Ok(ApiResponse<object>.Ok(new
            {
                CooldownCount = cooldowns.Count,
                Providers = cooldowns
            }, cooldowns.Count == 0
                ? "Mọi AI provider đều sẵn sàng."
                : $"{cooldowns.Count} provider đang tạm tắt do hết quota/rate limit."));
        }

        /// <summary>
        /// Reset toàn bộ keyword: xoá tất cả PaperKeywords + Keywords và đặt lại IsAiProcessed=false
        /// cho mọi bài báo, để reprocess lại sạch bằng flow chuẩn (abstract + 70b).
        /// CẢNH BÁO: xoá toàn bộ keyword hiện có. Cần ?confirm=true để thực thi.
        /// </summary>
        /// <param name="confirm">
        /// bool - FE/admin truyền qua query string (?confirm=true) - Chốt an toàn, phải = true mới thực thi.
        /// </param>
        /// <returns>
        /// Chuỗi JSON (ApiResponse&lt;object&gt;) gửi cho FE. Thuộc tính Data bao gồm:
        /// - LinksDeleted (int): Số liên kết PaperKeyword đã xoá.
        /// - KeywordsDeleted (int): Số keyword đã xoá khỏi bảng Keywords.
        /// - PapersReset (int): Số bài báo được đặt lại IsAiProcessed=false.
        /// Trả 400 nếu thiếu confirm=true, 409 nếu đang có job reprocess chạy.
        /// </returns>
        [HttpPost("reset-keywords")]
        public async Task<IActionResult> ResetKeywords([FromQuery] bool confirm = false)
        {
            if (!confirm)
                return BadRequest(ApiResponse<object>.Fail(400,
                    "Thao tác này xoá TOÀN BỘ keyword. Gọi lại với ?confirm=true nếu chắc chắn."));

            if (_reprocessService.GetState().IsRunning)
                return Conflict(ApiResponse<object>.Fail(409,
                    "Đang có job reprocess chạy. Dừng/chờ job xong trước khi reset."));

            var linksDeleted = await _dbContext.PaperKeywords.ExecuteDeleteAsync();
            var keywordsDeleted = await _dbContext.Keywords.ExecuteDeleteAsync();
            var papersReset = await _dbContext.ResearchPapers
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.IsAiProcessed, false)
                    .SetProperty(p => p.UpdatedAt, (DateTime?)null));

            _logger.LogWarning("Reset keywords: xoá {Links} link, {Kw} keyword, reset {Papers} paper.",
                linksDeleted, keywordsDeleted, papersReset);

            return Ok(ApiResponse<object>.Ok(
                new { LinksDeleted = linksDeleted, KeywordsDeleted = keywordsDeleted, PapersReset = papersReset },
                $"Đã reset: xoá {linksDeleted} link, {keywordsDeleted} keyword, {papersReset} paper về chưa xử lý."));
        }

        /// <summary>
        /// Khởi động reprocess keyword cho TẤT CẢ bài chưa xử lý ở chế độ chạy nền.
        /// Trả về ngay, dùng GET /api/admin/reprocess-status để theo dõi tiến độ.
        /// </summary>
        /// <param name="delayMs">
        /// int - FE/admin truyền qua query string (?delayMs=0) - Delay giữa mỗi paper (ms), mặc định 4000.
        /// Với Ollama local (không rate-limit) nên đặt 0 để chạy nhanh nhất.
        /// </param>
        /// <returns>
        /// Chuỗi JSON (ApiResponse&lt;ReprocessJobState&gt;) gửi cho FE. Thuộc tính Data là trạng thái job (xem reprocess-status).
        /// HTTP 202 nếu job vừa khởi động, 409 nếu đã có job đang chạy.
        /// </returns>
        [HttpPost("reprocess-all")]
        public IActionResult ReprocessAll([FromQuery] int delayMs = 4000)
        {
            var started = _reprocessService.StartBackground(delayMs);
            if (!started)
                return Conflict(ApiResponse<object>.Fail(409, "Đã có job reprocess đang chạy. Xem /api/admin/reprocess-status."));

            return Accepted(ApiResponse<object>.Ok(_reprocessService.GetState(),
                "Đã khởi động reprocess-all chạy nền. Theo dõi qua /api/admin/reprocess-status."));
        }

        /// <summary>
        /// Xem tiến độ job đào keyword (reprocess) đang/đã chạy. FE poll endpoint này để vẽ thanh tiến độ.
        /// Không nhận tham số.
        /// </summary>
        /// <returns>
        /// Chuỗi JSON (ApiResponse&lt;ReprocessJobState&gt;) gửi cho FE. Thuộc tính Data bao gồm:
        /// - IsRunning (bool): true = đang chạy; false = đã xong/chưa chạy (FE dựa vào đây để biết kết thúc).
        /// - TotalAtStart (int): Tổng số bài cần xử lý lúc bắt đầu job.
        /// - Processed (int): Số bài đã đào ra keyword thành công.
        /// - Failed (int): Số bài thất bại (không abstract hoặc AI trả 0 keyword).
        /// - Remaining (int): Số bài còn lại chưa xử lý (giảm dần về 0).
        /// - StartedAtUtc (DateTime?): Thời điểm UTC bắt đầu, null nếu chưa từng chạy.
        /// - FinishedAtUtc (DateTime?): Thời điểm UTC kết thúc, null khi đang chạy.
        /// - LastError (string): Lỗi gần nhất nếu có, null nếu không.
        /// - StopReason (string): Lý do dừng ("Hoàn thành — không còn bài" = xong thật).
        /// </returns>
        [HttpGet("reprocess-status")]
        public IActionResult ReprocessStatus()
        {
            var state = _reprocessService.GetState();
            var msg = state.IsRunning
                ? $"Đang chạy: {state.Processed} xong, {state.Failed} thất bại, còn {state.Remaining}."
                : state.StartedAtUtc == null
                    ? "Chưa có job nào chạy."
                    : $"Đã xong: {state.Processed} xong, {state.Failed} thất bại. {state.StopReason}";
            return Ok(ApiResponse<object>.Ok(state, msg));
        }

        /// <summary>
        /// DEMO: chạy NGAY đúng quy trình weekly sync tự động (thay vì chờ 2h sáng thứ Hai).
        /// Fetch bài MỚI (date-based, năm gần) → tự kích hoạt đào keyword nền (Ollama). Không nhận tham số.
        /// Bật Ollama trước khi gọi để thấy keyword xuất hiện. Theo dõi tiếp qua /reprocess-status.
        /// </summary>
        /// <returns>
        /// Chuỗi JSON (ApiResponse&lt;object&gt;) gửi cho FE. Thuộc tính Data bao gồm:
        /// - Added (int): Số bài MỚI vừa fetch và thêm vào DB.
        /// - AlreadyExists (int): Số bài đã có sẵn (bỏ qua do dedup).
        /// - Errors (int): Số bài lỗi khi xử lý.
        /// - ReprocessStarted (bool): true = đã khởi động job đào keyword nền; false = job đang chạy sẵn.
        /// </returns>
        [HttpPost("run-weekly-now")]
        public async Task<IActionResult> RunWeeklyNow()
        {
            var fromYear = DateTime.UtcNow.Year - 1;

            // Bước 1: fetch bài mới (giống weekly tự động)
            var result = await _syncOrchestrator.RunSyncAsync(
                50, skipKeywords: true, fromYear: fromYear, minCitedExclusive: -1, recentFirst: true);

            // Bước 2: tự đào keyword nền (delayMs=0 vì Ollama local không rate-limit)
            var started = _reprocessService.StartBackground(delayMs: 0);

            return Ok(ApiResponse<object>.Ok(new
            {
                result.Added,
                result.AlreadyExists,
                result.Errors,
                ReprocessStarted = started
            }, $"Fetch {result.Added} bài mới ({result.AlreadyExists} đã có). " +
               (started ? "Đã bắt đầu đào keyword nền — theo dõi /reprocess-status." : "Reprocess đang chạy sẵn.")));
        }

        /// <summary>
        /// Backfill CÂN BẰNG theo năm: mỗi năm fromYear→toYear lấy tối đa perYear bài (cùng tiêu chí),
        /// để trend share/năm có mẫu đủ và fair. Fetch-only (skipKeywords), cộng dồn, dedup.
        /// </summary>
        /// <param name="perYear">
        /// int - FE/admin truyền qua query string (?perYear=2500) - Số bài tối đa mỗi năm, mặc định 2500 (trần OpenAlex 10,000/năm).
        /// </param>
        /// <param name="fromYear">
        /// int - FE/admin truyền qua query string (?fromYear=2022) - Năm bắt đầu, mặc định 2022.
        /// </param>
        /// <param name="toYear">
        /// int - FE/admin truyền qua query string (?toYear=2026) - Năm kết thúc, mặc định 2026.
        /// </param>
        /// <returns>
        /// Chuỗi JSON (ApiResponse&lt;object&gt;) gửi cho FE. Thuộc tính Data bao gồm:
        /// - Added (int): Số bài mới thêm vào DB.
        /// - AlreadyExists (int): Số bài đã có (bỏ qua do dedup).
        /// - NoTitle (int): Số bài bị bỏ vì không có tiêu đề.
        /// - Errors (int): Số bài lỗi khi xử lý.
        /// Trả 400 nếu perYear hoặc khoảng năm không hợp lệ.
        /// </returns>
        [HttpPost("backfill-balanced")]
        public async Task<IActionResult> BackfillBalanced(
            [FromQuery] int perYear = 2500,
            [FromQuery] int fromYear = 2022,
            [FromQuery] int toYear = 2026)
        {
            if (perYear < 1 || perYear > 10000)
                return BadRequest(ApiResponse<object>.Fail(400, "perYear phải từ 1 đến 10000."));
            if (fromYear < 2000 || toYear > 2026 || fromYear > toYear)
                return BadRequest(ApiResponse<object>.Fail(400, "Khoảng năm không hợp lệ."));

            var result = await _syncOrchestrator.RunBalancedBackfillAsync(perYear, fromYear, toYear, skipKeywords: true);

            return Ok(ApiResponse<object>.Ok(new
            {
                result.Added,
                result.AlreadyExists,
                result.NoTitle,
                result.Errors
            }, $"Backfill {fromYear}-{toYear} ({perYear}/năm): {result.Added} thêm mới, {result.AlreadyExists} đã có, {result.Errors} lỗi."));
        }

        /// <summary>
        /// Import SCImago CSV bằng cách ĐỌC THẲNG file từ đường dẫn đĩa trên máy chạy server.
        /// Tránh hoàn toàn việc upload qua multipart (hay lỗi trên Swagger).
        /// Chỉ dùng trong Development — file phải nằm trên cùng máy với app.
        /// </summary>
        /// <param name="path">
        /// string - FE/admin truyền qua query string (?path=...) - Đường dẫn tuyệt đối tới file CSV SCImago trên đĩa máy server.
        /// </param>
        /// <returns>
        /// Chuỗi JSON (ApiResponse&lt;object&gt;) gửi cho FE. Thuộc tính Data bao gồm:
        /// - TotalRowsRead (int): Số dòng đọc được từ CSV (không tính header).
        /// - UpdatedCount (int): Số journal được cập nhật Q-rank thành công.
        /// - SkippedCount (int): Số dòng bỏ qua do ISSN không khớp hoặc thiếu dữ liệu.
        /// Trả 400 nếu thiếu path hoặc file không tồn tại.
        /// </returns>
        [HttpPost("import-scimago-path")]
        public async Task<IActionResult> ImportScimagoFromPath([FromQuery] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return BadRequest(ApiResponse<object>.Fail(400, "Thiếu tham số 'path'."));

            if (!System.IO.File.Exists(path))
                return BadRequest(ApiResponse<object>.Fail(400, $"Không tìm thấy file: {path}"));

            _logger.LogInformation("Admin import SCImago từ path: {Path}", path);

            using var stream = System.IO.File.OpenRead(path);
            var result = await _scimagoImportService.ImportFromCsvAsync(stream);

            return Ok(ApiResponse<object>.Ok(new
            {
                result.TotalRowsRead,
                result.UpdatedCount,
                result.SkippedCount
            }, $"Import hoàn thành: {result.UpdatedCount} journals đã được cập nhật Q-rank."));
        }
    }
}

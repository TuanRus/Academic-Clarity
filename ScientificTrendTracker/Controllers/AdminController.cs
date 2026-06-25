using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Models.DTOs.Subscription;
using ScientificTrendTracker.Models.DTOs.SyncLog;
using ScientificTrendTracker.Models.DTOs.ActivityLog;
using ScientificTrendTracker.Models.DTOs.Paper;
using ScientificTrendTracker.Models.DTOs.Keyword;
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
        private readonly ISubscriptionService _subscriptionService;
        private readonly IApiSyncLogService _apiSyncLogService;
        private readonly IAdminActivityLogService _adminActivityLogService;
        private readonly IPaperService _paperService;
        private readonly IKeywordService _keywordService;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IScimagoImportService scimagoImportService,
            IKeywordExtractionService keywordExtractionService,
            ISyncOrchestratorService syncOrchestrator,
            IKeywordReprocessService reprocessService,
            ISubscriptionService subscriptionService,
            IApiSyncLogService apiSyncLogService,
            IAdminActivityLogService adminActivityLogService,
            IPaperService paperService,
            IKeywordService keywordService,
            AppDbContext dbContext,
            ILogger<AdminController> logger)
        {
            _scimagoImportService = scimagoImportService;
            _keywordExtractionService = keywordExtractionService;
            _syncOrchestrator = syncOrchestrator;
            _reprocessService = reprocessService;
            _subscriptionService = subscriptionService;
            _apiSyncLogService = apiSyncLogService;
            _adminActivityLogService = adminActivityLogService;
            _paperService = paperService;
            _keywordService = keywordService;
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

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "RESET_KEYWORDS", 
                $"Đã reset từ khóa hệ thống: xóa {linksDeleted} liên kết, {keywordsDeleted} từ khóa, đặt lại {papersReset} bài báo.", ip);

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
        public async Task<IActionResult> ReprocessAll([FromQuery] int delayMs = 4000)
        {
            var started = _reprocessService.StartBackground(delayMs);
            if (!started)
                return Conflict(ApiResponse<object>.Fail(409, "Đã có job reprocess đang chạy. Xem /api/admin/reprocess-status."));

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "REPROCESS_KEYWORDS", 
                $"Khởi chạy tiến trình đào từ khóa nền với delay {delayMs}ms.", ip);

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

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "RUN_WEEKLY_SYNC", 
                $"Chạy tay tiến trình đồng bộ hàng tuần: fetch {result.Added} bài mới, {result.AlreadyExists} bài trùng.", ip);

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

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "BACKFILL_BALANCED", 
                $"Chạy tác vụ nạp dữ liệu cân bằng năm {fromYear}-{toYear} (tối đa {perYear} bài/năm): thêm {result.Added} bài mới, {result.AlreadyExists} bài trùng.", ip);

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

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "IMPORT_SCIMAGO", 
                $"Nhập danh sách SCImago từ đường dẫn file: {path}. Đọc {result.TotalRowsRead} dòng, cập nhật {result.UpdatedCount} journal.", ip);

            return Ok(ApiResponse<object>.Ok(new
            {
                result.TotalRowsRead,
                result.UpdatedCount,
                result.SkippedCount
            }, $"Import hoàn thành: {result.UpdatedCount} journals đã được cập nhật Q-rank."));
        }

        /// <summary>
        /// Lấy toàn bộ danh sách gói cước hiện có trong hệ thống (bao gồm cả các gói đã khóa) để quản trị.
        /// </summary>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime tự động cung cấp.</param>
        /// <returns>
        /// Trả về đối tượng ApiResponse bọc danh sách AdminSubscriptionPlanDto.
        /// - isSuccess (bool): true nếu lấy danh sách thành công.
        /// - statusCode (int): 200 OK.
        /// - data (Array): Danh sách cấu hình đầy đủ các gói cước.
        /// </returns>
        [HttpGet("subscriptions/plans")]
        public async Task<IActionResult> GetPlansForAdminAsync(CancellationToken ct)
        {
            var result = await _subscriptionService.GetAllPlansForAdminAsync(ct);
            return Ok(ApiResponse<List<AdminSubscriptionPlanDto>>.Ok(result, "Lấy danh sách quản trị gói cước thành công."));
        }

        /// <summary>
        /// Tạo mới một gói cước dịch vụ (ví dụ: gói Premium, gói VIP...).
        /// </summary>
        /// <param name="dto">CreateSubscriptionPlanDto - NGUỒN: FE truyền qua Body (JSON) chứa PlanName, PriceAmount, DurationDays.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>
        /// Trả về đối tượng ApiResponse báo kết quả tạo mới.
        /// - isSuccess (bool): true nếu tạo thành công, false nếu bị trùng tên gói.
        /// - statusCode (int): 200 OK hoặc 400 Bad Request.
        /// </returns>
        [HttpPost("subscriptions/plans")]
        public async Task<IActionResult> CreatePlanAsync([FromBody] CreateSubscriptionPlanDto dto, CancellationToken ct)
        {
            var success = await _subscriptionService.CreatePlanAsync(dto, ct);
            if (!success)
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Tạo gói cước thất bại. Tên gói cước đã tồn tại trong hệ thống."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "CREATE_SUBSCRIPTION_PLAN", 
                $"Tạo gói cước mới: '{dto.PlanName}' với giá {dto.PriceAmount} VNĐ, thời hạn {dto.DurationDays} ngày.", ip);

            return Ok(ApiResponse<object>.Ok(null, "Tạo gói cước mới thành công!"));
        }

        /// <summary>
        /// Cập nhật thông tin chi tiết (tên, giá tiền, thời hạn, trạng thái kích hoạt) của một gói cước đang có.
        /// </summary>
        /// <param name="planId">Số nguyên Int - NGUỒN: FE truyền qua Route Parameter. ID của gói cước cần sửa.</param>
        /// <param name="dto">UpdateSubscriptionPlanDto - NGUỒN: FE truyền qua Body (JSON).</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>
        /// Trả về đối tượng ApiResponse thông báo kết quả.
        /// - isSuccess (bool): true nếu cập nhật thành công.
        /// - statusCode (int): 200 OK hoặc 400 Bad Request.
        /// </returns>
        [HttpPut("subscriptions/plans/{planId:int}")]
        public async Task<IActionResult> UpdatePlanAsync(int planId, [FromBody] UpdateSubscriptionPlanDto dto, CancellationToken ct)
        {
            var success = await _subscriptionService.UpdatePlanAsync(planId, dto, ct);
            if (!success)
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Cập nhật thất bại. Gói cước không tồn tại hoặc tên mới bị trùng với gói khác."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "UPDATE_SUBSCRIPTION_PLAN", 
                $"Cập nhật thông tin gói cước ID {planId} (Tên mới: '{dto.PlanName}', Giá mới: {dto.PriceAmount} VNĐ).", ip);

            return Ok(ApiResponse<object>.Ok(null, "Cập nhật thông tin gói cước thành công!"));
        }

        /// <summary>
        /// Thay đổi nhanh trạng thái hoạt động (bật kích hoạt hoặc ngưng kích hoạt/Soft Delete) của một gói cước.
        /// </summary>
        /// <param name="planId">Số nguyên Int - NGUỒN: FE truyền qua Route Parameter. ID của gói cước cần đổi trạng thái.</param>
        /// <param name="isActive">Boolean - NGUỒN: FE truyền qua Query string (?isActive=true/false).</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>
        /// Trả về đối tượng ApiResponse thông báo kết quả.
        /// - isSuccess (bool): true nếu đổi trạng thái thành công.
        /// - statusCode (int): 200 OK hoặc 400 Bad Request.
        /// </returns>
        [HttpPatch("subscriptions/plans/{planId:int}/toggle")]
        public async Task<IActionResult> TogglePlanStatusAsync(int planId, [FromQuery] bool isActive, CancellationToken ct)
        {
            var success = await _subscriptionService.TogglePlanStatusAsync(planId, isActive, ct);
            if (!success)
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Không tìm thấy gói cước với ID yêu cầu."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "TOGGLE_SUBSCRIPTION_PLAN", 
                $"Thay đổi trạng thái gói cước ID {planId} thành {(isActive ? "Hoạt động" : "Tạm ngưng")}.", ip);

            return Ok(ApiResponse<object>.Ok(null, $"Thay đổi trạng thái hoạt động của gói cước thành {isActive} thành công."));
        }

        /// <summary>
        /// Lấy danh sách lịch sử đồng bộ dữ liệu tự động của hệ thống (có phân trang).
        /// </summary>
        /// <param name="page">Số nguyên Int - NGUỒN: FE truyền lên qua Query String. Số trang hiện tại (mặc định = 1).</param>
        /// <param name="pageSize">Số nguyên Int - NGUỒN: FE truyền lên qua Query String. Số dòng trên trang (mặc định = 10, max = 100).</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime tự động cung cấp.</param>
        /// <returns>
        /// Trả về đối tượng ApiResponse bọc PagedResult của ApiSyncLogResponseDto.
        /// </returns>
        [HttpGet("sync-logs")]
        public async Task<IActionResult> GetSyncLogsAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        {
            var result = await _apiSyncLogService.GetSyncLogsAsync(page, pageSize, ct);
            return Ok(ApiResponse<PagedResult<ApiSyncLogResponseDto>>.Ok(result, "Lấy danh sách nhật ký đồng bộ thành công."));
        }

        /// <summary>
        /// Lấy danh sách nhật ký ghi nhận các hành động thay đổi dữ liệu của Admin (có phân trang).
        /// </summary>
        /// <param name="page">Số nguyên Int - NGUỒN: FE truyền lên qua Query String. Số trang hiện tại (mặc định = 1).</param>
        /// <param name="pageSize">Số nguyên Int - NGUỒN: FE truyền lên qua Query String. Số dòng trên trang (mặc định = 10, max = 100).</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime tự động cung cấp.</param>
        /// <returns>
        /// Trả về đối tượng ApiResponse bọc PagedResult của AdminActivityLogResponseDto.
        /// </returns>
        [HttpGet("activity-logs")]
        public async Task<IActionResult> GetActivityLogsAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        {
            var result = await _adminActivityLogService.GetActivityLogsAsync(page, pageSize, ct);
            return Ok(ApiResponse<PagedResult<AdminActivityLogResponseDto>>.Ok(result, "Lấy danh sách nhật ký hoạt động Admin thành công."));
        }

        #region Paper CRUD Admin

        /// <summary>
        /// Admin lấy danh sách các bài báo khoa học trong hệ thống (có phân trang và bộ lọc).
        /// </summary>
        /// <param name="search">Chuỗi String - NGUỒN: FE truyền lên qua Query. Từ khóa tìm kiếm theo tiêu đề.</param>
        /// <param name="year">Số nguyên Int - NGUỒN: FE truyền lên qua Query. Lọc theo năm xuất bản.</param>
        /// <param name="journalId">Chuỗi String - NGUỒN: FE truyền lên qua Query. Lọc theo mã tạp chí.</param>
        /// <param name="page">Số nguyên Int - NGUỒN: FE truyền qua Query. Số trang hiện tại (mặc định = 1).</param>
        /// <param name="pageSize">Số nguyên Int - NGUỒN: FE truyền qua Query. Số dòng/trang (mặc định = 10).</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>Trả về ApiResponse bọc PagedResult của PaperAdminDto.</returns>
        [HttpGet("papers")]
        public async Task<IActionResult> GetPapersAsync(
            [FromQuery] string search,
            [FromQuery] int? year,
            [FromQuery] string journalId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken ct = default)
        {
            var result = await _paperService.GetPapersForAdminAsync(search, year, journalId, page, pageSize, ct);
            return Ok(ApiResponse<PagedResult<PaperAdminDto>>.Ok(result, "Lấy danh sách bài báo thành công."));
        }

        /// <summary>
        /// Admin xem thông tin chi tiết của một bài báo cụ thể (bao gồm danh sách tác giả, từ khóa).
        /// </summary>
        /// <param name="paperId">Chuỗi String - NGUỒN: FE truyền qua Route Parameter. ID của bài báo.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>Trả về ApiResponse chứa thông tin chi tiết bài báo.</returns>
        [HttpGet("papers/{paperId}")]
        public async Task<IActionResult> GetPaperDetailAsync(string paperId, CancellationToken ct)
        {
            var result = await _paperService.GetPaperDetailAsync(paperId, ct);
            if (result == null)
            {
                return NotFound(ApiResponse<object>.Fail(404, $"Không tìm thấy bài báo với ID: {paperId}"));
            }
            return Ok(ApiResponse<PaperDetailDto>.Ok(result, "Lấy chi tiết bài báo thành công."));
        }

        /// <summary>
        /// Admin tự điền thông tin và thêm một bài báo hay vào hệ thống.
        /// </summary>
        /// <param name="dto">CreatePaperDto - NGUỒN: FE truyền lên qua Body (JSON).</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>Trả về ApiResponse thông báo kết quả tạo mới.</returns>
        [HttpPost("papers")]
        public async Task<IActionResult> CreatePaperAsync([FromBody] CreatePaperDto dto, CancellationToken ct)
        {
            var success = await _paperService.CreatePaperAsync(dto, ct);
            if (!success)
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Tạo bài báo thất bại. Tiêu đề bài báo có thể đã tồn tại."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "CREATE_PAPER", 
                $"Đăng tải bài báo thủ công: '{dto.Title}'. Tạp chí: '{dto.JournalName}'.", ip);

            return Ok(ApiResponse<object>.Ok(null, "Thêm mới bài báo thành công!"));
        }

        /// <summary>
        /// Admin cập nhật thông tin và cập nhật lại danh sách liên kết tác giả, từ khóa của bài báo.
        /// </summary>
        /// <param name="paperId">Chuỗi String - NGUỒN: FE truyền qua Route Parameter. ID bài báo cần sửa.</param>
        /// <param name="dto">UpdatePaperDto - NGUỒN: FE truyền lên qua Body (JSON).</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>Trả về ApiResponse thông báo kết quả.</returns>
        [HttpPut("papers/{paperId}")]
        public async Task<IActionResult> UpdatePaperAsync(string paperId, [FromBody] UpdatePaperDto dto, CancellationToken ct)
        {
            var success = await _paperService.UpdatePaperAsync(paperId, dto, ct);
            if (!success)
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Cập nhật bài báo thất bại. Không tìm thấy bài viết hoặc tiêu đề mới bị trùng lặp."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "UPDATE_PAPER", 
                $"Cập nhật bài báo thủ công ID: {paperId} (Tiêu đề mới: '{dto.Title}').", ip);

            return Ok(ApiResponse<object>.Ok(null, "Cập nhật thông tin bài báo thành công!"));
        }

        /// <summary>
        /// Admin thực hiện xóa một bài báo ra khỏi hệ thống (đồng thời dọn các bảng liên kết).
        /// </summary>
        /// <param name="paperId">Chuỗi String - NGUỒN: FE truyền qua Route Parameter. ID bài báo cần xóa.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>Trả về ApiResponse thông báo kết quả.</returns>
        [HttpDelete("papers/{paperId}")]
        public async Task<IActionResult> DeletePaperAsync(string paperId, CancellationToken ct)
        {
            var success = await _paperService.DeletePaperAsync(paperId, ct);
            if (!success)
            {
                return NotFound(ApiResponse<object>.Fail(404, "Không tìm thấy bài báo cần xóa."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "DELETE_PAPER", 
                $"Xóa bài báo thủ công ID: {paperId}.", ip);

            return Ok(ApiResponse<object>.Ok(null, "Xóa bài báo thành công!"));
        }

        #endregion

        #region Keyword CRUD Admin

        /// <summary>
        /// Admin lấy danh sách các từ khóa hiện có (có phân trang và tìm kiếm).
        /// </summary>
        /// <param name="search">Chuỗi String - NGUỒN: FE truyền qua Query. Tìm kiếm theo tên từ khóa.</param>
        /// <param name="page">Số nguyên Int - NGUỒN: FE truyền qua Query. Số trang hiện tại.</param>
        /// <param name="pageSize">Số nguyên Int - NGUỒN: FE truyền qua Query. Số dòng/trang.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>Trả về ApiResponse bọc PagedResult của KeywordAdminDto.</returns>
        [HttpGet("keywords")]
        public async Task<IActionResult> GetKeywordsAsync(
            [FromQuery] string search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken ct = default)
        {
            var result = await _keywordService.GetKeywordsForAdminAsync(search, page, pageSize, ct);
            return Ok(ApiResponse<PagedResult<KeywordAdminDto>>.Ok(result, "Lấy danh sách từ khóa thành công."));
        }

        /// <summary>
        /// Admin tạo mới một từ khóa trong danh mục hệ thống.
        /// </summary>
        /// <param name="dto">CreateKeywordDto - NGUỒN: FE truyền qua Body (JSON).</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>Trả về ApiResponse thông báo kết quả.</returns>
        [HttpPost("keywords")]
        public async Task<IActionResult> CreateKeywordAsync([FromBody] CreateKeywordDto dto, CancellationToken ct)
        {
            var success = await _keywordService.CreateKeywordAsync(dto, ct);
            if (!success)
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Tạo từ khóa thất bại. Tên từ khóa đã tồn tại."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "CREATE_KEYWORD", 
                $"Tạo mới từ khóa hệ thống: '{dto.KeywordName}'.", ip);

            return Ok(ApiResponse<object>.Ok(null, "Tạo từ khóa mới thành công!"));
        }

        /// <summary>
        /// Admin chỉnh sửa tên của một từ khóa có sẵn.
        /// </summary>
        /// <param name="keywordId">Chuỗi String - NGUỒN: FE truyền qua Route Parameter. ID của từ khóa.</param>
        /// <param name="dto">UpdateKeywordDto - NGUỒN: FE truyền qua Body (JSON).</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>Trả về ApiResponse thông báo kết quả.</returns>
        [HttpPut("keywords/{keywordId}")]
        public async Task<IActionResult> UpdateKeywordAsync(string keywordId, [FromBody] UpdateKeywordDto dto, CancellationToken ct)
        {
            var success = await _keywordService.UpdateKeywordAsync(keywordId, dto, ct);
            if (!success)
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Cập nhật từ khóa thất bại. Không tìm thấy ID hoặc tên mới bị trùng."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "UPDATE_KEYWORD", 
                $"Cập nhật từ khóa ID: {keywordId} thành: '{dto.KeywordName}'.", ip);

            return Ok(ApiResponse<object>.Ok(null, "Cập nhật tên từ khóa thành công!"));
        }

        /// <summary>
        /// Admin thực hiện xóa một từ khóa ra khỏi hệ thống (đồng thời dọn các bảng liên kết).
        /// </summary>
        /// <param name="keywordId">Chuỗi String - NGUỒN: FE truyền qua Route parameter. ID từ khóa cần xóa.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>Trả về ApiResponse thông báo kết quả.</returns>
        [HttpDelete("keywords/{keywordId}")]
        public async Task<IActionResult> DeleteKeywordAsync(string keywordId, CancellationToken ct)
        {
            var success = await _keywordService.DeleteKeywordAsync(keywordId, ct);
            if (!success)
            {
                return NotFound(ApiResponse<object>.Fail(404, "Không tìm thấy từ khóa cần xóa."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "DELETE_KEYWORD", 
                $"Xóa từ khóa hệ thống ID: {keywordId}.", ip);

            return Ok(ApiResponse<object>.Ok(null, "Xóa từ khóa thành công!"));
        }

        #endregion
    }
}

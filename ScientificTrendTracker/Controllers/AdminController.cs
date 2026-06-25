using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Models.DTOs.Subscription;
using ScientificTrendTracker.Models.DTOs.SyncLog;
using ScientificTrendTracker.Models.DTOs.ActivityLog;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly IScimagoImportService _scimagoImportService;
        private readonly ISyncOrchestratorService _syncOrchestrator;
        private readonly IKeywordReprocessService _reprocessService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IApiSyncLogService _apiSyncLogService;
        private readonly IAdminActivityLogService _adminActivityLogService;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IScimagoImportService scimagoImportService,
            ISyncOrchestratorService syncOrchestrator,
            IKeywordReprocessService reprocessService,
            ISubscriptionService subscriptionService,
            IApiSyncLogService apiSyncLogService,
            IAdminActivityLogService adminActivityLogService,
            AppDbContext dbContext,
            ILogger<AdminController> logger)
        {
            _scimagoImportService = scimagoImportService;
            _syncOrchestrator = syncOrchestrator;
            _reprocessService = reprocessService;
            _subscriptionService = subscriptionService;
            _apiSyncLogService = apiSyncLogService;
            _adminActivityLogService = adminActivityLogService;
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// [Admin · DEV] Liệt kê session MySQL đang kết nối (chẩn đoán deadlock/lock-wait).
        /// </summary>
        [HttpGet("db-sessions")]
        public async Task<IActionResult> DbSessions()
        {
            var rows = new List<object>();
            var conn = _dbContext.Database.GetDbConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, User, db, Command, Time, State, LEFT(COALESCE(Info,''),80) AS Info " +
                              "FROM information_schema.processlist ORDER BY Time DESC";
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                rows.Add(new { Id = r.GetValue(0), User = r.GetValue(1), Db = r.GetValue(2),
                    Command = r.GetValue(3), Time = r.GetValue(4), State = r.GetValue(5), Info = r.GetValue(6) });
            return Ok(ApiResponse<object>.Ok(rows, $"{rows.Count} session."));
        }

        /// <summary>
        /// [Admin · DEV] Kill các session MySQL 'Sleep' idle quá lâu (giữ khoá treo từ kết nối chết).
        /// KHÔNG kill session hiện tại. Dùng khi mark/purge bị 'Lock wait timeout'.
        /// </summary>
        [HttpPost("db-kill-idle")]
        public async Task<IActionResult> DbKillIdle([FromQuery] int minSleepSeconds = 30)
        {
            var conn = _dbContext.Database.GetDbConnection();
            await conn.OpenAsync();
            // Lấy id các session Sleep lâu (trừ chính kết nối này).
            var ids = new List<long>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT Id FROM information_schema.processlist " +
                                  $"WHERE Command='Sleep' AND Time >= {minSleepSeconds} AND Id <> CONNECTION_ID()";
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) ids.Add(Convert.ToInt64(r.GetValue(0)));
            }
            var killed = new List<long>();
            foreach (var id in ids)
            {
                try { using var k = conn.CreateCommand(); k.CommandText = $"KILL {id}"; await k.ExecuteNonQueryAsync(); killed.Add(id); }
                catch (Exception ex) { _logger.LogWarning("Không kill được session {Id}: {Msg}", id, ex.Message); }
            }
            _logger.LogWarning("DB-KILL-IDLE: đã kill {N} session treo.", killed.Count);
            return Ok(ApiResponse<object>.Ok(new { Killed = killed }, $"Đã kill {killed.Count} session idle."));
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
        [ApiExplorerSettings(IgnoreApi = true)]
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
        /// [Admin · DEV] Đặt LẠI IsAiProcessed=false cho mọi bài (GIỮ NGUYÊN keyword OpenAlex đang có)
        /// để reprocess-all đào keyword AI hybrid (dùng keyword OpenAlex làm seed). Khác reset-keywords
        /// ở chỗ KHÔNG xoá keyword nào. Dùng khi corpus đã có keyword OpenAlex nhưng chưa qua AI.
        /// </summary>
        /// <returns>ApiResponse&lt;object&gt;: số bài được đặt lại chưa-xử-lý.</returns>
        [HttpPost("mark-all-unprocessed")]
        public async Task<IActionResult> MarkAllUnprocessed()
        {
            if (_reprocessService.GetState().IsRunning)
                return Conflict(ApiResponse<object>.Fail(409, "Đang có job reprocess chạy. Chờ/dừng job xong trước."));

            // UPDATE hàng chục nghìn dòng trên DB NAS có thể > 30s mặc định → nâng timeout.
            _dbContext.Database.SetCommandTimeout(300);
            var n = await _dbContext.ResearchPapers
                .Where(p => p.IsAiProcessed)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsAiProcessed, false));

            _logger.LogWarning("MARK-ALL-UNPROCESSED: đặt lại {N} bài về chưa-xử-lý-AI (giữ keyword OpenAlex).", n);
            return Ok(ApiResponse<object>.Ok(new { PapersReset = n },
                $"Đã đặt lại {n} bài về chưa-xử-lý-AI. Giờ chạy reprocess-all để đào keyword AI hybrid."));
        }

        /// <summary>
        /// Khởi động reprocess keyword cho TẤT CẢ bài chưa xử lý ở chế độ chạy nền.
        /// Trả về ngay, dùng GET /api/admin/reprocess-status để theo dõi tiến độ.
        /// </summary>
        /// <returns>
        /// Chuỗi JSON (ApiResponse&lt;ReprocessJobState&gt;) gửi cho FE. Thuộc tính Data là trạng thái job (xem reprocess-status).
        /// HTTP 202 nếu job vừa khởi động, 409 nếu đã có job đang chạy.
        /// </returns>
        [HttpPost("reprocess-all")]
        public async Task<IActionResult> ReprocessAll()
        {
            var started = _reprocessService.StartBackground();
            if (!started)
                return Conflict(ApiResponse<object>.Fail(409, "Đã có job reprocess đang chạy. Xem /api/admin/reprocess-status."));

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "REPROCESS_KEYWORDS",
                "Khởi chạy tiến trình đào từ khóa nền (AI local).", ip);

            return Accepted(ApiResponse<object>.Ok(_reprocessService.GetState(),
                "Đã khởi động reprocess-all chạy nền. Theo dõi qua /api/admin/reprocess-status."));
        }

        /// <summary>
        /// [Admin · theo dõi] Tiến độ job đào keyword nền (sinh ra sau khi chạy sync). Poll để xem
        /// IsRunning, Processed, Failed, Remaining. Dùng kèm "run-weekly-now".
        /// </summary>
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
        /// [Admin · luồng phụ] Chạy NGAY quy trình sync OpenAlex hàng tuần (thay vì chờ lịch tự động):
        /// fetch bài mới (năm gần) vào DB → tự kích hoạt đào keyword nền. Bật Ollama trước để có keyword;
        /// theo dõi qua "reprocess-status".
        /// </summary>
        [HttpPost("run-weekly-now")]
        public async Task<IActionResult> RunWeeklyNow()
        {
            var fromYear = DateTime.UtcNow.Year - 1;

            // Bước 1: fetch bài mới (giống weekly tự động)
            var result = await _syncOrchestrator.RunSyncAsync(
                50, skipKeywords: true, fromYear: fromYear, minCitedExclusive: -1, recentFirst: true);

            // Bước 2: tự đào keyword nền (AI local)
            var started = _reprocessService.StartBackground();

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
            [FromQuery] int perYear = 1000,
            [FromQuery] int fromYear = 2022,
            [FromQuery] int toYear = 2026,
            [FromQuery] bool purge = false)
        {
            if (perYear < 1 || perYear > 10000)
                return BadRequest(ApiResponse<object>.Fail(400, "perYear phải từ 1 đến 10000."));
            if (fromYear < 2000 || toYear > DateTime.UtcNow.Year + 1 || fromYear > toYear)
                return BadRequest(ApiResponse<object>.Fail(400, "Khoảng năm không hợp lệ."));

            // REBUILD: purge=true → xoá sạch papers/keywords/authors/links/citations cũ (GIỮ Journals + Q-rank SCImago)
            // rồi fetch lại từ đầu bằng filter mới (English + primary CS field) → corpus 100% đúng chuẩn.
            if (purge)
            {
                await PurgeCorpusAsync();
                _logger.LogWarning("REBUILD: đã purge toàn bộ papers/keywords/authors (giữ Journals).");
            }

            var result = await _syncOrchestrator.RunBalancedBackfillAsync(perYear, fromYear, toYear, skipKeywords: true);

            return Ok(ApiResponse<object>.Ok(new
            {
                Purged = purge,
                result.Added,
                result.AlreadyExists,
                result.NoTitle,
                result.Errors
            }, $"{(purge ? "Rebuild" : "Backfill")} {fromYear}-{toYear} ({perYear}/năm): {result.Added} thêm mới, {result.AlreadyExists} đã có, {result.Errors} lỗi."));
        }

        /// <summary>
        /// Xoá sạch corpus (papers/keywords/authors/links/citations) theo đúng thứ tự FK.
        /// GIỮ Journals + Q-rank SCImago + accounts/users. Dọn cả bảng legacy PublicationTrends
        /// (FK tới Keywords, không có trong EF model) bằng raw SQL trước khi xoá Keywords/Authors.
        /// </summary>
        private async Task PurgeCorpusAsync()
        {
            // Bảng legacy chỉ tồn tại trong DB (không có trong DbContext) — xoá rows trước để khỏi vướng FK.
            // Bọc try/catch: DB nào không có bảng này thì bỏ qua.
            try { await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM PublicationTrends"); }
            catch (Exception ex) { _logger.LogWarning("Bỏ qua dọn PublicationTrends: {Msg}", ex.Message); }

            await _dbContext.PaperCitations.ExecuteDeleteAsync();
            await _dbContext.PaperKeywords.ExecuteDeleteAsync();
            await _dbContext.PaperAuthors.ExecuteDeleteAsync();
            await _dbContext.ResearchPapers.ExecuteDeleteAsync();
            await _dbContext.Keywords.ExecuteDeleteAsync();
            await _dbContext.Authors.ExecuteDeleteAsync();
        }

        /// <summary>
        /// [Admin · DEV - XOÁ TRƯỚC DEPLOY] GỘP 1 BƯỚC: xoá sạch corpus cũ (papers/keywords/authors/links/citations,
        /// GIỮ Journals + Q-rank + accounts) → fetch lại keyword OpenAlex → tự khởi động đào keyword hybrid AI nền.
        /// Tương đương: backfill-balanced?purge=true  +  reprocess-all. KHÔNG đụng WeeklySync.
        /// Theo dõi tiến độ đào AI qua GET /api/admin/reprocess-status.
        /// </summary>
        /// <param name="perYear">int - Số bài tối đa mỗi năm (top-cited). Mặc định 1000.</param>
        /// <param name="fromYear">int - Năm bắt đầu. Mặc định 2020.</param>
        /// <param name="toYear">int - Năm kết thúc. Mặc định 2026.</param>
        /// <returns>ApiResponse&lt;object&gt;: kết quả fetch + đã khởi động job đào AI hay chưa.</returns>
        [HttpPost("rebuild-corpus")]
        public async Task<IActionResult> RebuildCorpus(
            [FromQuery] int perYear = 1000,
            [FromQuery] int fromYear = 2020,
            [FromQuery] int toYear = 2026)
        {
            if (perYear < 1 || perYear > 10000)
                return BadRequest(ApiResponse<object>.Fail(400, "perYear phải từ 1 đến 10000."));
            if (fromYear < 2000 || toYear > DateTime.UtcNow.Year + 1 || fromYear > toYear)
                return BadRequest(ApiResponse<object>.Fail(400, "Khoảng năm không hợp lệ."));
            if (_reprocessService.GetState().IsRunning)
                return Conflict(ApiResponse<object>.Fail(409,
                    "Đang có job reprocess chạy. Chờ/dừng job xong trước khi rebuild."));

            // B1: PURGE (giữ Journals + Q-rank + accounts) → fetch lại keyword OpenAlex.
            await PurgeCorpusAsync();
            _logger.LogWarning("REBUILD-CORPUS: đã purge toàn bộ papers/keywords/authors (giữ Journals + accounts).");

            var result = await _syncOrchestrator.RunBalancedBackfillAsync(perYear, fromYear, toYear, skipKeywords: true);

            // B2: tự khởi động đào keyword hybrid AI (Ollama + seed OpenAlex) chạy nền.
            var started = _reprocessService.StartBackground();

            // Ghi nhật ký hoạt động admin (theo convention của team).
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "REBUILD_CORPUS",
                $"Chạy rebuild corpus năm {fromYear}-{toYear} (tối đa {perYear} bài/năm): thêm {result.Added} bài mới, {result.AlreadyExists} bài trùng.", ip);

            return Ok(ApiResponse<object>.Ok(new
            {
                result.Added,
                result.AlreadyExists,
                result.NoTitle,
                result.Errors,
                AiMiningStarted = started
            }, $"Rebuild {fromYear}-{toYear} ({perYear}/năm): {result.Added} bài + keyword OpenAlex. " +
               (started ? "Đã bắt đầu đào keyword AI nền — theo dõi /api/admin/reprocess-status."
                        : "Job đào AI đang chạy sẵn.")));
        }

        /// <summary>
        /// [Admin · luồng phụ] Cập nhật Q-rank tạp chí từ file CSV SCImago: đọc thẳng file theo đường dẫn
        /// trên máy server (?path=...). Dùng khi SCImago ra bảng xếp hạng năm mới (vd cuối năm) để nạp bù.
        /// </summary>
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
    }
}

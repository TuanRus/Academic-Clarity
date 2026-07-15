using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Services;
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
        private readonly ISyncOrchestratorService _syncOrchestrator;
        private readonly IKeywordReprocessService _reprocessService;
        private readonly ITopicBackfillService _topicBackfillService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IApiSyncLogService _apiSyncLogService;
        private readonly IAdminActivityLogService _adminActivityLogService;
        private readonly IPaperService _paperService;
        private readonly IKeywordService _keywordService;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<AdminController> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ISyncProgressTracker _syncProgress;

        public AdminController(
            IScimagoImportService scimagoImportService,
            ISyncOrchestratorService syncOrchestrator,
            IKeywordReprocessService reprocessService,
            ITopicBackfillService topicBackfillService,
            ISubscriptionService subscriptionService,
            IApiSyncLogService apiSyncLogService,
            IAdminActivityLogService adminActivityLogService,
            IPaperService paperService,
            IKeywordService keywordService,
            AppDbContext dbContext,
            ILogger<AdminController> logger,
            IServiceScopeFactory scopeFactory,
            ISyncProgressTracker syncProgress)
        {
            _scimagoImportService = scimagoImportService;
            _syncOrchestrator = syncOrchestrator;
            _reprocessService = reprocessService;
            _topicBackfillService = topicBackfillService;
            _subscriptionService = subscriptionService;
            _apiSyncLogService = apiSyncLogService;
            _adminActivityLogService = adminActivityLogService;
            _paperService = paperService;
            _keywordService = keywordService;
            _dbContext = dbContext;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _syncProgress = syncProgress;
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
            return Ok(ApiResponse<object>.Ok(new { Killed = killed }, $"Killed {killed.Count} idle session(s)."));
        }

        /// <summary>[Admin] Danh sách user (cho trang quản trị Users), phân trang.</summary>
        [HttpGet("users")]
        public async Task<IActionResult> ListUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 50;

            var query = _dbContext.Users.OrderByDescending(u => u.CreateAt);
            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(u => new
                {
                    u.UserId,
                    u.Email,
                    u.Fullname,
                    u.RoleId,
                    u.IsActive,
                    u.AccountTag,
                    u.Institution,
                    u.CreateAt,
                    u.LastLoginAt
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(new
            {
                items,
                totalCount = total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            }, $"{total} user."));
        }

        /// <summary>[Admin] Đổi vai trò (RoleId) của một user, lưu xuống DB.</summary>
        [HttpPut("users/{userId:int}/role")]
        public async Task<IActionResult> UpdateUserRole(int userId, [FromBody] ScientificTrendTracker.Models.DTOs.UpdateUserRoleDto dto, CancellationToken ct)
        {
            bool roleExists = await _dbContext.Roles.AnyAsync(r => r.RoleId == dto.RoleId, ct);
            if (!roleExists)
                return BadRequest(ApiResponse<object>.Fail(400, "Invalid RoleId."));

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
            if (user == null)
                return NotFound(ApiResponse<object>.Fail(404, "User not found."));

            user.RoleId = dto.RoleId;
            user.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "UPDATE_USER_ROLE",
                $"Changed role of user {userId} ({user.Email}) to RoleId {dto.RoleId}.", ip);

            return Ok(ApiResponse<object>.Ok(null, "User role changed successfully."));
        }

        /// <summary>[Admin] Bật/tắt trạng thái hoạt động (suspend/activate) của một user.</summary>
        [HttpPatch("users/{userId:int}/status")]
        public async Task<IActionResult> UpdateUserStatus(int userId, [FromQuery] bool isActive, CancellationToken ct)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
            if (user == null)
                return NotFound(ApiResponse<object>.Fail(404, "User not found."));

            user.IsActive = isActive;
            user.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "UPDATE_USER_STATUS",
                $"{(isActive ? "Activated" : "Suspended")} user {userId} ({user.Email}).", ip);

            return Ok(ApiResponse<object>.Ok(null, $"Updated user status to {(isActive ? "ACTIVE" : "SUSPENDED")}."));
        }

        /// <summary>[Admin · DEV] Chèn 4 role chuẩn (1-admin, 2-researcher, 3-edu user, 4-regular user) nếu chưa có.</summary>
        [HttpPost("seed-roles")]
        public async Task<IActionResult> SeedRolesInsert()
        {
            if (await _dbContext.Roles.AnyAsync())
                return Conflict(ApiResponse<object>.Fail(409, "Roles table already contains data — will not re-insert."));

            await _dbContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO Roles (role_id, role_name, description) VALUES " +
                "(1, 'admin', 'Quản trị hệ thống'), " +
                "(2, 'researcher', 'Nhà nghiên cứu'), " +
                "(3, 'edu user', 'Người dùng giáo dục'), " +
                "(4, 'regular user', 'Người dùng thường')");

            var roles = await _dbContext.Roles.Select(r => new { r.RoleId, r.RoleName }).ToListAsync();
            return Ok(ApiResponse<object>.Ok(roles, $"Inserted {roles.Count} role(s)."));
        }

        /// <summary>[Admin · DEV] Liệt kê các role (RoleId + RoleName) để biết id admin.</summary>
        [HttpGet("seed-roles")]
        public async Task<IActionResult> SeedRoles()
        {
            var roles = await _dbContext.Roles
                .Select(r => new { r.RoleId, r.RoleName, r.Description })
                .ToListAsync();
            return Ok(ApiResponse<object>.Ok(roles, $"{roles.Count} role."));
        }

        /// <summary>[Admin · DEV] Tạo nhanh 1 user (hash BCrypt) để test. Xoá endpoint này trước deploy.</summary>
        [HttpPost("seed-user")]
        public async Task<IActionResult> SeedUser(
            [FromQuery] string email,
            [FromQuery] string name,
            [FromQuery] int roleId,
            [FromQuery] string password = "Test@12345")
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name))
                return BadRequest(ApiResponse<object>.Fail(400, "Email or name is missing."));
            if (await _dbContext.Users.AnyAsync(u => u.Email == email))
                return Conflict(ApiResponse<object>.Fail(409, $"Email '{email}' already exists."));

            var user = new Models.Entities.User
            {
                Email = email,
                Fullname = name,
                RoleId = roleId,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                AccountTag = roleId == 3,
                IsActive = true,
                CreateAt = DateTime.UtcNow
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            return Ok(ApiResponse<object>.Ok(new { user.UserId, user.Email, user.RoleId, password },
                $"Created user '{email}' (roleId={roleId}, password='{password}')."));
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
                    "This deletes ALL keywords. Please call with ?confirm=true if sure."));

            if (_reprocessService.GetState().IsRunning)
                return Conflict(ApiResponse<object>.Fail(409,
                    "Reprocessing job is running. Stop/wait before resetting."));

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
                $"Reset system keywords: deleted {linksDeleted} links, {keywordsDeleted} keywords, reset {papersReset} papers.", ip);

            return Ok(ApiResponse<object>.Ok(
                new { LinksDeleted = linksDeleted, KeywordsDeleted = keywordsDeleted, PapersReset = papersReset },
                $"Reset complete: deleted {linksDeleted} links, {keywordsDeleted} keywords, reset {papersReset} papers."));
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
                return Conflict(ApiResponse<object>.Fail(409, "A reprocessing job is currently running. Please wait or stop it first."));

            // UPDATE hàng chục nghìn dòng trên DB NAS có thể > 30s mặc định → nâng timeout.
            _dbContext.Database.SetCommandTimeout(300);
            var n = await _dbContext.ResearchPapers
                .Where(p => p.IsAiProcessed)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsAiProcessed, false));

            _logger.LogWarning("MARK-ALL-UNPROCESSED: đặt lại {N} bài về chưa-xử-lý-AI (giữ keyword OpenAlex).", n);
            return Ok(ApiResponse<object>.Ok(new { PapersReset = n },
                $"Reset {n} papers to unprocessed-AI. Run reprocess-all for AI hybrid keywords."));
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
                return Conflict(ApiResponse<object>.Fail(409, "A reprocessing job is already running. Check status at /api/admin/reprocess-status."));

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "REPROCESS_KEYWORDS",
                "Started background keyword extraction process (local AI).", ip);

            return Accepted(ApiResponse<object>.Ok(_reprocessService.GetState(),
                "Started background reprocess. Track progress via /api/admin/reprocess-status."));
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
                ? $"Running: {state.Processed} done, {state.Failed} failed, {state.Remaining} remaining."
                : state.StartedAtUtc == null
                    ? "No job has run yet."
                    : $"Completed: {state.Processed} done, {state.Failed} failed. {state.StopReason}";
            return Ok(ApiResponse<object>.Ok(state, msg));
        }

        /// <summary>[Admin] Cào lại Topic cho TOÀN BỘ bài báo (đào từ đầu) — chạy nền, tách khỏi job keyword.</summary>
        [HttpPost("backfill-topics")]
        public IActionResult BackfillTopics()
        {
            var started = _topicBackfillService.StartBackground();
            if (!started)
                return Conflict(ApiResponse<object>.Fail(409, "A topic backfilling job is already running. Check status at /api/admin/backfill-topics-status."));
            return Accepted(ApiResponse<object>.Ok(_topicBackfillService.GetState(),
                "Started background topic backfill. Track via /api/admin/backfill-topics-status."));
        }

        /// <summary>[Admin · theo dõi] Tiến độ job cào lại Topic.</summary>
        [HttpGet("backfill-topics-status")]
        public IActionResult BackfillTopicsStatus()
        {
            var s = _topicBackfillService.GetState();
            var msg = s.IsRunning
                ? $"Running: {s.Processed}/{s.Total} papers, updated {s.Updated}, failed {s.Failed}."
                : s.StartedAtUtc == null ? "Not run yet."
                : $"Completed: {s.Processed}/{s.Total}, updated {s.Updated}, failed {s.Failed}.";
            return Ok(ApiResponse<object>.Ok(s, msg));
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
                $"Manually ran weekly sync process: fetched {result.Added} new papers, {result.AlreadyExists} duplicates.", ip);

            return Ok(ApiResponse<object>.Ok(new
            {
                result.Added,
                result.AlreadyExists,
                result.Errors,
                ReprocessStarted = started
            }, $"Fetched {result.Added} new papers ({result.AlreadyExists} already existed). " +
               (started ? "Started background AI mining — track via /reprocess-status." : "Reprocess already running.")));
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
                return BadRequest(ApiResponse<object>.Fail(400, "perYear must be between 1 and 10000."));
            if (fromYear < 2000 || toYear > DateTime.UtcNow.Year + 1 || fromYear > toYear)
                return BadRequest(ApiResponse<object>.Fail(400, "Invalid year range."));

            // REBUILD: purge=true → xoá sạch papers/keywords/authors/links/citations cũ (GIỮ Journals + Q-rank SCImago)
            // rồi fetch lại từ đầu bằng filter mới (English + primary CS field) → corpus 100% đúng chuẩn.
            if (purge)
            {
                await PurgeCorpusAsync();
                _logger.LogWarning("REBUILD: purged all papers/keywords/authors (kept Journals).");
            }

            var result = await _syncOrchestrator.RunBalancedBackfillAsync(perYear, fromYear, toYear, skipKeywords: true);

            return Ok(ApiResponse<object>.Ok(new
            {
                Purged = purge,
                result.Added,
                result.AlreadyExists,
                result.NoTitle,
                result.Errors
            }, $"{(purge ? "Rebuild" : "Backfill")} {fromYear}-{toYear} ({perYear}/year): {result.Added} added, {result.AlreadyExists} exists, {result.Errors} errors."));
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
            catch (Exception ex) { _logger.LogWarning("Skipping PublicationTrends cleanup: {Msg}", ex.Message); }

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
                return BadRequest(ApiResponse<object>.Fail(400, "perYear must be between 1 and 10000."));
            if (fromYear < 2000 || toYear > DateTime.UtcNow.Year + 1 || fromYear > toYear)
                return BadRequest(ApiResponse<object>.Fail(400, "Invalid year range."));
            if (_reprocessService.GetState().IsRunning)
                return Conflict(ApiResponse<object>.Fail(409,
                    "Reprocess job is running. Stop/wait before rebuild."));

            // B1: PURGE (giữ Journals + Q-rank + accounts) → fetch lại keyword OpenAlex.
            await PurgeCorpusAsync();
            _logger.LogWarning("REBUILD-CORPUS: purged all papers/keywords/authors (kept Journals + accounts).");

            var result = await _syncOrchestrator.RunBalancedBackfillAsync(perYear, fromYear, toYear, skipKeywords: true);

            // B2: tự khởi động đào keyword hybrid AI (Ollama + seed OpenAlex) chạy nền.
            var started = _reprocessService.StartBackground();

            // Ghi nhật ký hoạt động admin (theo convention của team).
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "REBUILD_CORPUS",
                $"Ran corpus rebuild for years {fromYear}-{toYear} (up to {perYear} papers/year): added {result.Added} new papers, {result.AlreadyExists} duplicates.", ip);

            return Ok(ApiResponse<object>.Ok(new
            {
                result.Added,
                result.AlreadyExists,
                result.NoTitle,
                result.Errors,
                AiMiningStarted = started
            }, $"Rebuild {fromYear}-{toYear} ({perYear}/year): {result.Added} added. " +
               (started ? "Started AI mining — track via /api/admin/reprocess-status."
                        : "AI mining job already running.")));
        }

        /// <summary>
        /// [Admin · luồng phụ] Cập nhật Q-rank tạp chí từ file CSV SCImago: đọc thẳng file theo đường dẫn
        /// trên máy server (?path=...). Dùng khi SCImago ra bảng xếp hạng năm mới (vd cuối năm) để nạp bù.
        /// </summary>
        [HttpPost("import-scimago-path")]
        public async Task<IActionResult> ImportScimagoFromPath([FromQuery] string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return BadRequest(ApiResponse<object>.Fail(400, "Missing 'path' parameter."));

            if (!System.IO.File.Exists(path))
                return BadRequest(ApiResponse<object>.Fail(400, $"File not found: {path}"));

            _logger.LogInformation("Admin import SCImago from path: {Path}", path);

            using var stream = System.IO.File.OpenRead(path);
            var result = await _scimagoImportService.ImportFromCsvAsync(stream);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "IMPORT_SCIMAGO", 
                $"Imported SCImago list from file path: {path}. Read {result.TotalRowsRead} rows, updated {result.UpdatedCount} journals.", ip);

            return Ok(ApiResponse<object>.Ok(new
            {
                result.TotalRowsRead,
                result.UpdatedCount,
                result.SkippedCount
            }, $"Import complete: {result.UpdatedCount} journals updated."));
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
            return Ok(ApiResponse<List<AdminSubscriptionPlanDto>>.Ok(result, "Subscription plan list retrieved successfully."));
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
                return BadRequest(ApiResponse<object>.Fail(400, "Failed to create subscription plan. The plan name already exists."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "CREATE_SUBSCRIPTION_PLAN", 
                $"Created new subscription plan: '{dto.PlanName}' priced at {dto.PriceAmount} VND, duration {dto.DurationDays} days.", ip);

            return Ok(ApiResponse<object>.Ok(null, "Subscription plan created successfully!"));
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
                return BadRequest(ApiResponse<object>.Fail(400, "Failed to update plan. Plan not found or name conflict."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "UPDATE_SUBSCRIPTION_PLAN", 
                $"Updated subscription plan ID {planId} (New name: '{dto.PlanName}', New price: {dto.PriceAmount} VND).", ip);

            return Ok(ApiResponse<object>.Ok(null, "Subscription plan updated successfully!"));
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
                return BadRequest(ApiResponse<object>.Fail(400, "Subscription plan not found."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "TOGGLE_SUBSCRIPTION_PLAN", 
                $"Changed status of subscription plan ID {planId} to {(isActive ? "Active" : "Inactive")}.", ip);

            return Ok(ApiResponse<object>.Ok(null, $"Changed plan status to {isActive} successfully."));
        }

        /// <summary>
        /// Xoá cứng một gói cước (chỉ khi chưa có người đăng ký). Nếu đã dùng nên dùng toggle để khóa.
        /// </summary>
        /// <param name="planId">Số nguyên Int - NGUỒN: Route Parameter. ID gói cước cần xoá.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>200 nếu xoá thành công; 404 nếu không tồn tại; 409 nếu gói đã có người đăng ký.</returns>
        [HttpDelete("subscriptions/plans/{planId:int}")]
        public async Task<IActionResult> DeletePlanAsync(int planId, CancellationToken ct)
        {
            var result = await _subscriptionService.DeletePlanAsync(planId, ct);
            if (result == "NOT_FOUND")
                return NotFound(ApiResponse<object>.Fail(404, "Plan not found."));
            if (result == "IN_USE")
                return Conflict(ApiResponse<object>.Fail(409, "Plan has active subscribers — cannot delete. Use Disable to lock it."));

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "DELETE_SUBSCRIPTION_PLAN",
                $"Deleted subscription plan ID {planId}.", ip);

            return Ok(ApiResponse<object>.Ok(null, "Subscription plan deleted successfully."));
        }

        /// <summary>
        /// Lấy lịch sử giao dịch (mỗi UserSubscription = 1 lần mua gói) để hiển thị bảng Recent Payment Transactions.
        /// </summary>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>Danh sách TransactionRowDto, mới nhất trước.</returns>
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactionsAsync(CancellationToken ct)
        {
            var rows = await (
                from s in _dbContext.UserSubscriptions
                join u in _dbContext.Users on s.UserId equals u.UserId
                join p in _dbContext.SubscriptionPlans on s.PlanId equals p.PlanId
                orderby s.CreatedAt descending
                select new TransactionRowDto
                {
                    SubscriptionId = s.SubscriptionId,
                    CustomerName = u.Fullname,
                    CustomerEmail = u.Email,
                    PlanName = p.PlanName,
                    Amount = s.PaidAmount ?? p.PriceAmount, // ưu tiên số thực trả; fallback giá gốc cho dữ liệu cũ

                    Status = s.Status,
                    CreatedAt = s.CreatedAt
                }
            ).Take(100).ToListAsync(ct);

            return Ok(ApiResponse<List<TransactionRowDto>>.Ok(rows, $"Found {rows.Count} transaction(s)."));
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
            return Ok(ApiResponse<PagedResult<ApiSyncLogResponseDto>>.Ok(result, "Sync logs retrieved successfully."));
        }

        /// <summary>
        /// Dọn dẹp lịch sử sync: xoá các nhật ký KHÔNG thêm bài nào (RecordsImported = 0),
        /// bỏ qua lần đang chạy (Status = 'running'). Trả về số dòng đã xoá.
        /// </summary>
        [HttpDelete("sync-logs/empty")]
        public async Task<IActionResult> DeleteEmptySyncLogsAsync(CancellationToken ct)
        {
            var emptyLogs = await _dbContext.ApiSyncLogs
                .Where(l => l.RecordsImported == 0 && l.Status != "running")
                .ToListAsync(ct);

            if (emptyLogs.Count == 0)
                return Ok(ApiResponse<object>.Ok(new { deleted = 0 }, "No empty sync logs to delete."));

            _dbContext.ApiSyncLogs.RemoveRange(emptyLogs);
            await _dbContext.SaveChangesAsync(ct);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "CLEANUP_SYNC_LOGS",
                $"Deleted {emptyLogs.Count} sync log(s) with 0 imported papers.", ip);

            return Ok(ApiResponse<object>.Ok(new { deleted = emptyLogs.Count },
                $"Deleted {emptyLogs.Count} sync log(s) with 0 imported papers."));
        }

        /// <summary>
        /// Chi tiết 1 lần sync: liệt kê các bài báo được THÊM trong khung thời gian của lần sync đó
        /// (đối chiếu theo ResearchPaper.CreatedAt nằm giữa SyncStartedAt và SyncFinishedAt).
        /// </summary>
        /// <param name="id">int - Route param - SyncLogId cần xem chi tiết.</param>
        /// <param name="ct">CancellationToken - ASP.NET Core runtime.</param>
        /// <returns>ApiResponse chứa danh sách SyncedPaperDto; 404 nếu không tìm thấy nhật ký.</returns>
        [HttpGet("sync-logs/{id:int}/papers")]
        public async Task<IActionResult> GetSyncedPapersAsync(int id, CancellationToken ct)
        {
            var log = await _dbContext.ApiSyncLogs.FirstOrDefaultAsync(l => l.SyncLogId == id, ct);
            if (log == null)
                return NotFound(ApiResponse<object>.Fail(404, $"Sync log #{id} not found."));

            // Lần sync không thêm bài nào (thất bại/mồ côi/không có bài mới) → không có gì để liệt kê.
            if (log.RecordsImported <= 0)
            {
                return Ok(ApiResponse<object>.Ok(
                    new { log.SyncLogId, log.Status, log.RecordsImported, count = 0, papers = new List<SyncedPaperDto>() },
                    $"Sync #{id} imported 0 papers."));
            }

            // Đối chiếu theo khung thời gian: bài tạo trong [SyncStartedAt, SyncFinishedAt],
            // giới hạn số dòng theo RecordsImported để bám sát số bài lần sync này thực thêm.
            var start = log.SyncStartedAt;
            var end = log.SyncFinishedAt ?? DateTime.UtcNow;

            var papers = await _dbContext.ResearchPapers
                .Where(p => p.CreatedAt >= start && p.CreatedAt <= end)
                .OrderByDescending(p => p.CreatedAt)
                .Take(Math.Min(log.RecordsImported, 1000))
                .Select(p => new SyncedPaperDto
                {
                    PaperId = p.PaperId,
                    Title = p.Title,
                    PublicationYear = p.PublicationYear,
                    OpenAlexId = p.OpenAlexId,
                    SourceUrl = p.SourceUrl,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync(ct);

            return Ok(ApiResponse<object>.Ok(
                new { log.SyncLogId, log.Status, log.RecordsImported, count = papers.Count, papers },
                $"Sync #{id}: {papers.Count} papers found."));
        }

        /// <summary>
        /// Bắt đầu 1 lần sync CHẠY NỀN (trả về ngay) để theo dõi realtime qua /sync/progress.
        /// Guard: từ chối nếu đang có sync chạy.
        /// </summary>
        /// <param name="maxPages">int - Query - Số trang OpenAlex tối đa (1..50, mặc định 2 cho nhanh).</param>
        [HttpPost("sync/start")]
        public IActionResult StartLiveSync([FromQuery] int maxPages = 2, [FromQuery] int? fromYear = null)
        {
            if (_syncProgress.IsRunning)
                return Conflict(ApiResponse<object>.Fail(409, "A sync process is already running."));

            if (maxPages < 1) maxPages = 1;
            if (maxPages > 50) maxPages = 50;
            var effFromYear = fromYear ?? (DateTime.UtcNow.Year - 1);

            // Chạy nền: scope của request kết thúc ngay sau response nên phải tạo scope riêng.
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var orch = scope.ServiceProvider.GetRequiredService<ISyncOrchestratorService>();
                try
                {
                    await orch.RunSyncAsync(maxPages, skipKeywords: true, fromYear: effFromYear,
                        minCitedExclusive: -1, recentFirst: true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi sync nền.");
                }
            });

            return Ok(ApiResponse<object>.Ok(new { started = true, maxPages },
                "Sync started. Track via /sync/progress."));
        }

        /// <summary>Tiến độ sync realtime (time - paper - status) để FE poll hiển thị.</summary>
        [HttpGet("sync/progress")]
        public IActionResult GetSyncProgress()
        {
            return Ok(ApiResponse<object>.Ok(_syncProgress.Snapshot(), "Current sync progress."));
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
            return Ok(ApiResponse<PagedResult<AdminActivityLogResponseDto>>.Ok(result, "Activity logs retrieved successfully."));
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
            return Ok(ApiResponse<PagedResult<PaperAdminDto>>.Ok(result, "Papers list retrieved successfully."));
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
                return NotFound(ApiResponse<object>.Fail(404, $"Paper not found with ID: {paperId}"));
            }
            return Ok(ApiResponse<PaperDetailDto>.Ok(result, "Paper details retrieved successfully."));
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
                return BadRequest(ApiResponse<object>.Fail(400, "Failed to create paper. The paper title may already exist."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "CREATE_PAPER", 
                $"Manually uploaded paper: '{dto.Title}'. Journal: '{dto.JournalName}'.", ip);

            return Ok(ApiResponse<object>.Ok(null, "Paper added successfully!"));
        }

        /// <summary>
        /// Admin dán link (OpenAlex / DOI) để hệ thống TỰ ĐỘNG fetch metadata và thêm bài báo vào kho sưu tầm.
        /// </summary>
        /// <param name="dto">CreatePaperFromLinkDto - NGUỒN: FE truyền lên qua Body (JSON) - Chứa link bài báo.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>ApiResponse: 200 nếu thêm thành công; 400 nếu link sai/không lấy được dữ liệu/trùng bài.</returns>
        [HttpPost("papers/from-link")]
        public async Task<IActionResult> CreatePaperFromLinkAsync([FromBody] CreatePaperFromLinkDto dto, CancellationToken ct)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Link))
            {
                return BadRequest(ApiResponse<object>.Fail(400, "Please enter a paper link (OpenAlex or DOI)."));
            }

            var (success, message) = await _paperService.CreatePaperFromLinkAsync(dto.Link, ct);
            if (!success)
            {
                return BadRequest(ApiResponse<object>.Fail(400, message));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "CREATE_PAPER_FROM_LINK",
                $"Automatically added paper from link: '{dto.Link}'.", ip);

            return Ok(ApiResponse<object>.Ok(null, message));
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
                return BadRequest(ApiResponse<object>.Fail(400, "Failed to update paper. Paper not found or the new title is a duplicate."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "UPDATE_PAPER", 
                $"Manually updated paper ID: {paperId} (New title: '{dto.Title}').", ip);

            return Ok(ApiResponse<object>.Ok(null, "Paper updated successfully!"));
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
                return NotFound(ApiResponse<object>.Fail(404, "Paper to delete not found."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "DELETE_PAPER", 
                $"Manually deleted paper ID: {paperId}.", ip);

            return Ok(ApiResponse<object>.Ok(null, "Paper deleted successfully!"));
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
            return Ok(ApiResponse<PagedResult<KeywordAdminDto>>.Ok(result, "Keywords list retrieved successfully."));
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
                return BadRequest(ApiResponse<object>.Fail(400, "Failed to create keyword. Keyword name already exists."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "CREATE_KEYWORD", 
                $"Created new system keyword: '{dto.KeywordName}'.", ip);

            return Ok(ApiResponse<object>.Ok(null, "Keyword created successfully!"));
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
                return BadRequest(ApiResponse<object>.Fail(400, "Failed to update keyword. ID not found or the new name is a duplicate."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "UPDATE_KEYWORD", 
                $"Updated keyword ID: {keywordId} to: '{dto.KeywordName}'.", ip);

            return Ok(ApiResponse<object>.Ok(null, "Keyword updated successfully!"));
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
                return NotFound(ApiResponse<object>.Fail(404, "Keyword to delete not found."));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            await _adminActivityLogService.LogActivityAsync(1, "DELETE_KEYWORD", 
                $"Deleted system keyword ID: {keywordId}.", ip);

            return Ok(ApiResponse<object>.Ok(null, "Keyword deleted successfully!"));
        }

        #endregion
    }
}

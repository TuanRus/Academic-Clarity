using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Singleton: chạy nền reprocess keyword cho toàn bộ bài báo chưa xử lý (IsAiProcessed=false).
    /// Dùng IServiceScopeFactory để lấy scoped service (DbContext, AI service) trong background task.
    /// </summary>
    public class KeywordReprocessService : IKeywordReprocessService
    {
        private const int BatchSize = 50; // số bài load mỗi lần query DB

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<KeywordReprocessService> _logger;
        private readonly ReprocessJobState _state = new();
        private readonly object _lock = new();

        public KeywordReprocessService(IServiceScopeFactory scopeFactory, ILogger<KeywordReprocessService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public bool StartBackground()
        {
            lock (_lock)
            {
                if (_state.IsRunning) return false;

                // Reset state cho lần chạy mới
                _state.IsRunning = true;
                _state.Processed = 0;
                _state.Failed = 0;
                _state.StartedAtUtc = DateTime.UtcNow;
                _state.FinishedAtUtc = null;
                _state.LastError = null;
                _state.StopReason = null;
            }

            _ = Task.Run(RunAsync);
            return true;
        }

        public ReprocessJobState GetState()
        {
            // Trả về bản copy để tránh caller đọc state đang bị mutate
            lock (_lock)
            {
                return new ReprocessJobState
                {
                    IsRunning = _state.IsRunning,
                    TotalAtStart = _state.TotalAtStart,
                    Processed = _state.Processed,
                    Failed = _state.Failed,
                    Remaining = _state.Remaining,
                    StartedAtUtc = _state.StartedAtUtc,
                    FinishedAtUtc = _state.FinishedAtUtc,
                    LastError = _state.LastError,
                    StopReason = _state.StopReason
                };
            }
        }

        /// <summary>
        /// Vòng lặp chạy nền: lấy từng batch bài IsAiProcessed=false → fetch abstract → trích keyword (AI local)
        /// → lưu DB, cập nhật _state. Lỗi 1 bài thì đánh dấu failed và chạy tiếp.
        /// </summary>
        private async Task RunAsync()
        {
            _logger.LogInformation("=== Bắt đầu reprocess-all (background) ===");
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var openAlexService = scope.ServiceProvider.GetRequiredService<IOpenAlexService>();
                var keywordService = scope.ServiceProvider.GetRequiredService<IKeywordExtractionService>();

                var totalAtStart = await dbContext.ResearchPapers.CountAsync(p => !p.IsAiProcessed);
                lock (_lock)
                {
                    _state.TotalAtStart = totalAtStart;
                    _state.Remaining = totalAtStart;
                }

                while (true)
                {
                    // Tính lại Remaining mỗi batch — vì sync có thể thêm bài mới song song
                    var remainingNow = await dbContext.ResearchPapers.CountAsync(p => !p.IsAiProcessed);
                    lock (_lock) { _state.Remaining = remainingNow; }

                    var papers = await dbContext.ResearchPapers
                        .Where(p => !p.IsAiProcessed)
                        .OrderBy(p => p.CreatedAt)
                        .Take(BatchSize)
                        .ToListAsync();

                    if (papers.Count == 0)
                    {
                        SetStopReason("Hoàn thành — không còn bài chưa xử lý.");
                        break;
                    }

                    foreach (var paper in papers)
                    {
                        try
                        {
                            // Tải abstract (in-memory, KHÔNG lưu DB) → trích keyword → bỏ abstract
                            var abstract_ = await openAlexService.FetchAbstractByIdAsync(paper.OpenAlexId);

                            // Không có abstract → không đoán bừa từ title, đánh dấu processed để khỏi retry
                            if (string.IsNullOrWhiteSpace(abstract_))
                            {
                                paper.IsAiProcessed = true;
                                paper.UpdatedAt = DateTime.UtcNow;
                                await dbContext.SaveChangesAsync();
                                lock (_lock) { _state.Failed++; }
                                continue;
                            }

                            // Seed = keyword OpenAlex sẵn có của bài → AI bám controlled vocabulary.
                            var seedKeywords = await dbContext.PaperKeywords
                                .Where(pk => pk.PaperId == paper.PaperId)
                                .Select(pk => pk.Keyword.KeywordName)
                                .ToListAsync();
                            var keywords = await keywordService.ExtractKeywordsAsync(abstract_, paper.Title, seedKeywords);

                            if (keywords.Count > 0)
                                await SaveKeywordsAsync(dbContext, paper.PaperId, keywords);

                            paper.IsAiProcessed = true;
                            paper.UpdatedAt = DateTime.UtcNow;
                            await dbContext.SaveChangesAsync();

                            lock (_lock)
                            {
                                if (keywords.Count > 0) _state.Processed++;
                                else _state.Failed++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Reprocess-all lỗi paper {PaperId}: {Message}", paper.PaperId, ex.Message);
                            dbContext.ChangeTracker.Clear();
                            lock (_lock) { _state.Failed++; _state.LastError = ex.Message; }
                        }
                    }

                    _logger.LogInformation("Reprocess-all tiến độ: processed={P} failed={F} remaining={R}",
                        _state.Processed, _state.Failed, _state.Remaining);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reprocess-all dừng do lỗi: {Message}", ex.Message);
                SetStopReason($"Dừng do lỗi: {ex.Message}");
                lock (_lock) { _state.LastError = ex.Message; }
            }
            finally
            {
                lock (_lock)
                {
                    _state.IsRunning = false;
                    _state.FinishedAtUtc = DateTime.UtcNow;
                }
                _logger.LogInformation("=== Reprocess-all kết thúc: processed={P} failed={F} ===",
                    _state.Processed, _state.Failed);
            }
        }

        /// <summary>Ghi lý do dừng job vào state (thread-safe) để hiển thị ở reprocess-status.</summary>
        private void SetStopReason(string reason)
        {
            lock (_lock) { _state.StopReason = reason; }
        }

        /// <summary>
        /// Lưu keyword của 1 bài: tạo Keyword mới nếu chưa có (dedup theo tên), rồi link qua PaperKeywords.
        /// </summary>
        private static async Task SaveKeywordsAsync(AppDbContext dbContext, string paperId, List<string> keywords)
        {
            var names = keywords.Distinct().ToList();
            if (names.Count == 0) return;

            // 1 query: nạp các keyword đã tồn tại (thay vì FirstOrDefault từng cái — tránh N+1)
            var existing = await dbContext.Keywords
                .Where(k => names.Contains(k.KeywordName))
                .ToDictionaryAsync(k => k.KeywordName, k => k);

            // Tạo keyword mới cho phần chưa có
            foreach (var name in names)
            {
                if (existing.ContainsKey(name)) continue;
                var kw = new Models.Entities.Keyword
                {
                    KeywordId = Guid.NewGuid().ToString("N")[..20],
                    KeywordName = name,
                    CreatedAt = DateTime.UtcNow
                };
                dbContext.Keywords.Add(kw);
                existing[name] = kw;
            }
            await dbContext.SaveChangesAsync(); // lưu keyword mới để có KeywordId

            // 1 query: các link đã tồn tại cho bài này
            var keywordIds = existing.Values.Select(k => k.KeywordId).ToList();
            var linkedIds = (await dbContext.PaperKeywords
                .Where(pk => pk.PaperId == paperId && keywordIds.Contains(pk.KeywordId))
                .Select(pk => pk.KeywordId)
                .ToListAsync()).ToHashSet();

            foreach (var name in names)
            {
                var id = existing[name].KeywordId;
                if (!linkedIds.Contains(id))
                    dbContext.PaperKeywords.Add(new Models.Entities.PaperKeyword { PaperId = paperId, KeywordId = id });
            }
            await dbContext.SaveChangesAsync();
        }
    }
}

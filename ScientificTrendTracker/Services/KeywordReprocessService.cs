using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Singleton: chạy nền reprocess keyword cho toàn bộ bài báo chưa xử lý.
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

        public bool StartBackground(int delayMs = 4000)
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

            _ = Task.Run(() => RunAsync(delayMs));
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
        /// Vòng lặp chạy nền: lấy từng batch bài IsAiProcessed=false → fetch abstract → trích keyword → lưu DB,
        /// cập nhật _state. Khi mọi provider cooldown phút thì nghỉ rồi retry; cooldown ngày thì dừng hẳn.
        /// </summary>
        /// <param name="delayMs">int - StartBackground truyền vào - Delay giữa mỗi paper (ms); 0 cho Ollama local.</param>
        private async Task RunAsync(int delayMs)
        {
            _logger.LogInformation("=== Bắt đầu reprocess-all (background) ===");
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var openAlexService = scope.ServiceProvider.GetRequiredService<IOpenAlexService>();
                var keywordService = scope.ServiceProvider.GetRequiredService<IKeywordExtractionService>();

                lock (_lock)
                {
                    _state.TotalAtStart = dbContext.ResearchPapers.Count(p => !p.IsAiProcessed);
                    _state.Remaining = _state.TotalAtStart;
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

                    var pausedByQuota = false;

                    foreach (var paper in papers)
                    {
                        var calledAi = false; // chỉ delay khi đã thực sự gọi AI
                        try
                        {
                            // Flow: tải abstract (in-memory, KHÔNG lưu DB) → extract keyword → bỏ abstract
                            var abstract_ = await openAlexService.FetchAbstractByIdAsync(paper.OpenAlexId);

                            // Không có abstract → không đoán bừa từ title, đánh dấu processed để khỏi retry
                            if (string.IsNullOrWhiteSpace(abstract_))
                            {
                                paper.IsAiProcessed = true;
                                paper.UpdatedAt = DateTime.UtcNow;
                                await dbContext.SaveChangesAsync();
                                lock (_lock) { _state.Failed++; }
                                continue; // không gọi AI → finally không delay
                            }

                            calledAi = true; // sắp gọi AI
                            var keywords = await keywordService.ExtractKeywordsAsync(abstract_, paper.Title);

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
                        catch (AllProvidersExhaustedException)
                        {
                            // Mọi provider đang cooldown. Phân biệt: rate-limit PHÚT (đợi 1 chút rồi chạy tiếp)
                            // vs hết quota NGÀY (dừng hẳn tới 00:00 UTC). KHÔNG đánh dấu bài processed.
                            var cooldowns = keywordService.GetProviderCooldowns();
                            var now = DateTime.UtcNow;
                            // Thời điểm provider sớm nhất hồi lại. Rỗng (race: vừa hết cooldown) → retry ngay.
                            var earliest = cooldowns.Count > 0 ? cooldowns.Values.Min() : now;
                            var wait = earliest - now;

                            // Ngưỡng: cooldown ≤ 10 phút coi là rate-limit phút → đợi rồi retry chính bài này.
                            if (wait <= TimeSpan.FromMinutes(10))
                            {
                                var sleep = wait > TimeSpan.Zero ? wait + TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(2);
                                _logger.LogWarning("Mọi provider chạm rate-limit phút. Nghỉ {Sec:F0}s rồi chạy tiếp (bài {PaperId} retry).",
                                    sleep.TotalSeconds, paper.PaperId);
                                await Task.Delay(sleep);
                                // break foreach (không set pausedByQuota) → vòng while re-query đúng các bài chưa xử lý → retry
                                break;
                            }

                            // Cooldown dài (kiểu ngày) → dừng hẳn, chờ quota reset
                            _logger.LogWarning("Reprocess-all DỪNG: mọi AI provider hết quota NGÀY. Bài {PaperId} sẽ chạy lại sau.", paper.PaperId);
                            SetStopReason("Tạm dừng do hết quota AI — chạy lại sau khi quota reset (00:00 UTC).");
                            pausedByQuota = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Reprocess-all lỗi paper {PaperId}: {Message}", paper.PaperId, ex.Message);
                            dbContext.ChangeTracker.Clear();
                            lock (_lock) { _state.Failed++; _state.LastError = ex.Message; }
                        }
                        finally
                        {
                            // ĐẶT Ở FINALLY: luôn delay sau khi gọi AI, kể cả khi try ném exception → không spin
                            if (calledAi)
                                await Task.Delay(delayMs);
                        }
                    }

                    if (pausedByQuota) break;

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
        /// <param name="reason">string - Caller truyền vào - Mô tả lý do dừng (vd "Hoàn thành — không còn bài").</param>
        private void SetStopReason(string reason)
        {
            lock (_lock) { _state.StopReason = reason; }
        }

        /// <summary>
        /// Lưu keyword của 1 bài: tạo Keyword mới nếu chưa có (dedup theo tên), rồi link qua PaperKeywords.
        /// </summary>
        /// <param name="dbContext">AppDbContext - Truyền từ scope của job nền - DbContext để ghi DB.</param>
        /// <param name="paperId">string - Caller truyền vào - PaperId của bài để tạo link.</param>
        /// <param name="keywords">List&lt;string&gt; - AI trả về - Keyword đã chuẩn hóa (lowercase-hyphen).</param>
        private static async Task SaveKeywordsAsync(AppDbContext dbContext, string paperId, List<string> keywords)
        {
            foreach (var keywordName in keywords)
            {
                var keyword = await dbContext.Keywords
                    .FirstOrDefaultAsync(k => k.KeywordName == keywordName);

                if (keyword == null)
                {
                    keyword = new Keyword
                    {
                        KeywordId = Guid.NewGuid().ToString("N")[..20],
                        KeywordName = keywordName,
                        CreatedAt = DateTime.UtcNow
                    };
                    dbContext.Keywords.Add(keyword);
                    await dbContext.SaveChangesAsync();
                }

                var linkExists = await dbContext.PaperKeywords
                    .AnyAsync(pk => pk.PaperId == paperId && pk.KeywordId == keyword.KeywordId);

                if (!linkExists)
                {
                    dbContext.PaperKeywords.Add(new PaperKeyword
                    {
                        PaperId = paperId,
                        KeywordId = keyword.KeywordId
                    });
                }
            }
            await dbContext.SaveChangesAsync();
        }
    }
}

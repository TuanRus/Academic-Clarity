using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Singleton chạy nền: cào lại Topic cho TOÀN BỘ bài báo (đào từ đầu), tuần tự, từng batch.
    /// Mỗi bài 1 request nhẹ tới OpenAlex (select=primary_topic).
    /// </summary>
    public class TopicBackfillService : ITopicBackfillService
    {
        private const int BatchSize = 100;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TopicBackfillService> _logger;
        private readonly TopicBackfillState _state = new();
        private readonly object _lock = new();

        public TopicBackfillService(IServiceScopeFactory scopeFactory, ILogger<TopicBackfillService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public bool StartBackground()
        {
            lock (_lock)
            {
                if (_state.IsRunning) return false;
                _state.IsRunning = true;
                _state.Total = 0;
                _state.Processed = 0;
                _state.Updated = 0;
                _state.Failed = 0;
                _state.StartedAtUtc = DateTime.UtcNow;
                _state.FinishedAtUtc = null;
                _state.LastError = null;
            }
            _ = Task.Run(RunAsync);
            return true;
        }

        public TopicBackfillState GetState()
        {
            lock (_lock)
            {
                return new TopicBackfillState
                {
                    IsRunning = _state.IsRunning,
                    Total = _state.Total,
                    Processed = _state.Processed,
                    Updated = _state.Updated,
                    Failed = _state.Failed,
                    StartedAtUtc = _state.StartedAtUtc,
                    FinishedAtUtc = _state.FinishedAtUtc,
                    LastError = _state.LastError
                };
            }
        }

        private async Task RunAsync()
        {
            _logger.LogInformation("=== Bắt đầu backfill Topic (toàn bộ bài) ===");
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var openAlex = scope.ServiceProvider.GetRequiredService<IOpenAlexService>();

                var total = await dbContext.ResearchPapers.CountAsync();
                lock (_lock) { _state.Total = total; }

                string lastPaperId = null; // phân trang theo PaperId để ổn định
                while (true)
                {
                    var query = dbContext.ResearchPapers.AsQueryable();
                    if (lastPaperId != null)
                        query = query.Where(p => string.Compare(p.PaperId, lastPaperId) > 0);

                    var papers = await query
                        .OrderBy(p => p.PaperId)
                        .Take(BatchSize)
                        .Select(p => new { p.PaperId, p.OpenAlexId, p.Topic })
                        .ToListAsync();

                    if (papers.Count == 0) break;

                    foreach (var p in papers)
                    {
                        try
                        {
                            var topic = await openAlex.FetchTopicByIdAsync(p.OpenAlexId);
                            if (!string.IsNullOrWhiteSpace(topic))
                            {
                                var trimmed = topic.Length > 255 ? topic[..255] : topic;
                                if (trimmed != p.Topic)
                                {
                                    await dbContext.ResearchPapers
                                        .Where(x => x.PaperId == p.PaperId)
                                        .ExecuteUpdateAsync(s => s
                                            .SetProperty(x => x.Topic, trimmed)
                                            .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
                                    lock (_lock) { _state.Updated++; }
                                }
                            }
                            lock (_lock) { _state.Processed++; }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Backfill topic lỗi paper {PaperId}", p.PaperId);
                            lock (_lock) { _state.Failed++; _state.Processed++; _state.LastError = ex.Message; }
                        }
                    }

                    lastPaperId = papers[^1].PaperId;
                    _logger.LogInformation("Backfill Topic: processed={P} updated={U} failed={F}",
                        _state.Processed, _state.Updated, _state.Failed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backfill Topic dừng do lỗi: {Message}", ex.Message);
                lock (_lock) { _state.LastError = ex.Message; }
            }
            finally
            {
                lock (_lock) { _state.IsRunning = false; _state.FinishedAtUtc = DateTime.UtcNow; }
                _logger.LogInformation("=== Backfill Topic xong: processed={P} updated={U} failed={F} ===",
                    _state.Processed, _state.Updated, _state.Failed);
            }
        }
    }
}

using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.BackgroundServices
{
    public class WeeklySyncService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WeeklySyncService> _logger;

        private static readonly TimeSpan SyncTime = new(2, 0, 0);
        private const DayOfWeek SyncDay = DayOfWeek.Monday;
        private const int MaxPagesPerSync = 50;

        public WeeklySyncService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<WeeklySyncService> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Cờ bật/tắt sync tự động. Mặc định false → chỉ MÁY BACKEND chính (đặt WeeklySync:Enabled=true
            // trong appsettings.Development.json) mới chạy sync nền. Tránh đồng đội clone về vô tình chạy đè DB.
            if (!_configuration.GetValue("WeeklySync:Enabled", false))
            {
                _logger.LogInformation("WeeklySync:Enabled=false → KHÔNG chạy sync tự động (chỉ chạy thủ công qua /api/admin/run-weekly-now).");
                return;
            }

            _logger.LogInformation("WeeklySyncService đã khởi động (sync tự động BẬT).");

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = CalculateDelayUntilNextSync();
                _logger.LogInformation("Sync tiếp theo sau {Hours:F1} giờ.", delay.TotalHours);

                await Task.Delay(delay, stoppingToken);

                if (stoppingToken.IsCancellationRequested) break;

                using var scope = _scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<ISyncOrchestratorService>();
                var reprocess = scope.ServiceProvider.GetRequiredService<IKeywordReprocessService>();

                // Weekly sync TỰ ĐỘNG: vét bài MỚI (sort theo ngày, bỏ lọc citation vì bài mới chưa kịp được trích dẫn),
                // chỉ năm gần đây (year-1 → nay) để bắt xu hướng mới nổi. Fetch-only ở bước này.
                var recentFromYear = DateTime.UtcNow.Year - 1;
                await orchestrator.RunSyncAsync(
                    MaxPagesPerSync, skipKeywords: true,
                    fromYear: recentFromYear, minCitedExclusive: -1, recentFirst: true,
                    cancellationToken: stoppingToken);

                // Sau khi fetch xong → tự đào keyword cho bài mới (delayMs=0 vì Ollama local không rate-limit).
                // BỀN với AI tắt: nếu Ollama off, job reprocess dừng êm (không crash), bài giữ IsAiProcessed=false
                // và sẽ được đào ở lần sync sau hoặc khi chạy reprocess-all thủ công.
                reprocess.StartBackground(delayMs: 0);
                _logger.LogInformation("Weekly sync xong fetch, đã kích hoạt đào keyword nền cho bài mới.");
            }
        }

        private TimeSpan CalculateDelayUntilNextSync()
        {
            var now = DateTime.UtcNow;
            var daysUntilMonday = ((int)SyncDay - (int)now.DayOfWeek + 7) % 7;
            var nextSync = now.Date.AddDays(daysUntilMonday).Add(SyncTime);
            if (nextSync <= now) nextSync = nextSync.AddDays(7);
            return nextSync - now;
        }
    }
}

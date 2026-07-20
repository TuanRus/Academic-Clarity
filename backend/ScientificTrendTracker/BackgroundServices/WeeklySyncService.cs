using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.BackgroundServices
{
    public class WeeklySyncService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WeeklySyncService> _logger;
        private readonly IKeywordReprocessService _reprocessService; // singleton — tách keyword AI sau sync

        private static readonly TimeSpan SyncTime = new(2, 0, 0);
        private const DayOfWeek SyncDay = DayOfWeek.Monday;
        private const int MaxPagesPerSync = 50;

        public WeeklySyncService(IServiceScopeFactory scopeFactory, IConfiguration configuration,
            ILogger<WeeklySyncService> logger, IKeywordReprocessService reprocessService)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
            _reprocessService = reprocessService;
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

                // Weekly sync TỰ ĐỘNG: vét bài MỚI (sort theo ngày, bỏ lọc citation vì bài mới chưa kịp được trích dẫn),
                // chỉ năm gần đây (year-1 → nay) để bắt xu hướng mới nổi.
                var recentFromYear = DateTime.UtcNow.Year - 1;
                await orchestrator.RunSyncAsync(
                    MaxPagesPerSync, skipKeywords: true,
                    fromYear: recentFromYear, minCitedExclusive: -1, recentFirst: true,
                    cancellationToken: stoppingToken);

                // Sau khi fetch bài mới → kích hoạt Background Keyword Reprocessing Job (AI local)
                // để tách keyword cho bài mới, giống luồng sync thủ công.
                _reprocessService.StartBackground();
                _logger.LogInformation("Weekly sync hoàn tất fetch — đã kích hoạt tách keyword AI nền.");
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

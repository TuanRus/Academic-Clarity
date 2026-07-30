using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.BackgroundServices
{
    /// <summary>
    /// Định kỳ đối soát các đơn thanh toán còn treo ở trạng thái PENDING.
    /// Khi user tạo link PayOS nhưng không hoàn tất (đóng tab, không quay lại ReturnUrl và
    /// webhook cũng không tới) thì bản ghi UserSubscription kẹt PENDING mãi mãi. Service này
    /// hỏi lại PayOS trạng thái thật của những đơn đã quá hạn để thăng cấp (nếu đã PAID) hoặc
    /// chuyển CANCELLED, nhờ đó bảng "Recent Payment Transactions" không còn PENDING treo.
    /// </summary>
    public class PendingPaymentReconcileService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PendingPaymentReconcileService> _logger;

        // Chu kỳ quét. PayOS link thường hết hạn trong ~15 phút nên quét mỗi 5 phút là đủ nhanh.
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        // Chỉ đụng đơn đã tạo lâu hơn khoảng này để không hủy nhầm đơn user còn đang thanh toán dở.
        private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

        public PendingPaymentReconcileService(
            IServiceScopeFactory scopeFactory,
            ILogger<PendingPaymentReconcileService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PendingPaymentReconcileService đã khởi động (quét đơn PENDING mỗi {Minutes} phút).", Interval.TotalMinutes);

            using var timer = new PeriodicTimer(Interval);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
                    var changed = await paymentService.ReconcilePendingPaymentsAsync(StaleAfter, stoppingToken);
                    if (changed > 0)
                    {
                        _logger.LogInformation("PendingPaymentReconcile: đã xử lý {Count} đơn PENDING treo.", changed);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PendingPaymentReconcile: lỗi trong một nhịp đối soát, sẽ thử lại nhịp sau.");
                }

                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
        }
    }
}

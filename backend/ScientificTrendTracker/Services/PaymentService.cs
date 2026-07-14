using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PayOS;
using PayOS.Models.Webhooks;
using PayOS.Models.V2.PaymentRequests;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Lớp thực thi nghiệp vụ tính toán giá, kết nối cổng PayOS và xử lý thăng cấp tài khoản.
    /// </summary>
    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentService> _logger;
        private readonly PayOSClient _payOS;
        private readonly IMemoryCache _cache;

        /// <summary>Map orderCode -> (userId, amount) tạm thời để xác nhận khi user quay về ReturnUrl (không cần webhook).</summary>
        private static string OrderCacheKey(long orderCode) => $"payorder:{orderCode}";

        /// <summary>
        /// Khởi tạo dịch vụ xử lý thanh toán và tiêm các ngữ cảnh cấu hình cần thiết, bao gồm cả PayOS SDK.
        /// </summary>
        /// <param name="context">AppDbContext - DI từ hệ thống - Ngữ cảnh kết nối DB MySQL.</param>
        /// <param name="configuration">IConfiguration - DI từ hệ thống - Đọc cấu hình appsettings.json.</param>
        /// <param name="logger">ILogger&lt;PaymentService&gt; - DI từ hệ thống - Ghi vết log nghiệp vụ.</param>
        /// <param name="payOS">PayOSClient - DI từ hệ thống - Đối tượng kết nối trực tiếp cổng thanh toán PayOS.</param>
        public PaymentService(
            AppDbContext context,
            IConfiguration configuration,
            ILogger<PaymentService> logger,
            PayOSClient payOS,
            IMemoryCache cache)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _payOS = payOS;
            _cache = cache;
        }

        /// <summary>
        /// Kiểm tra thông tin gói, áp giá ưu đãi học thuật và gọi cổng PayOS thật để sinh liên kết thanh toán VietQR.
        /// </summary>
        /// <param name="userId">int - Từ token giải mã ở Controller - ID của người dùng đăng ký mua gói.</param>
        /// <param name="request">CreatePaymentLinkRequestDto - Từ Request Body của Frontend - Thông tin gói dịch vụ muốn mua.</param>
        /// <returns>PaymentLinkResponseDto chứa thông tin link thanh toán PayOS và mã QR, hoặc null nếu gói hoặc người dùng không tồn tại.</returns>
        public async Task<PaymentLinkResponseDto?> CreatePaymentLinkAsync(
            int userId, 
            CreatePaymentLinkRequestDto request)
        {
            // Sử dụng AsNoTracking để tránh phát sinh chi phí theo dõi thực thể cho luồng chỉ đọc gói dịch vụ tĩnh.
            var plan = await _context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PlanId == request.PlanId && p.IsActive);

            if (plan == null)
            {
                _logger.LogWarning("PlanId {PlanId} không tồn tại hoặc đã bị khóa.", request.PlanId);
                return null; 
            }

            // Lấy UserId, AccountTag, RoleId để xác định đối tượng học thuật được ưu đãi.
            var user = await _context.Users
                .AsNoTracking()
                .Select(u => new { u.UserId, u.AccountTag, u.RoleId })
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null) return null;

            decimal finalAmount = plan.PriceAmount;

            // Giảm 50% nếu là đối tượng học thuật: email .edu (AccountTag) HOẶC role Researcher(2)/Student-edu(3).
            bool isAcademic = user.AccountTag || user.RoleId == 2 || user.RoleId == 3;
            if (isAcademic)
            {
                finalAmount = plan.PriceAmount * 0.5m;
            }

            // Tạo mã hóa đơn dạng int từ Ticks đảm bảo không vượt quá giới hạn của PayOS (yêu cầu orderCode <= 99999999).
            long orderCode = DateTime.UtcNow.Ticks % 99999999;

            var item = new PaymentLinkItem
            {
                Name = plan.PlanName,
                Quantity = 1,
                Price = (long)finalAmount
            };
            var items = new List<PaymentLinkItem> { item };

            var paymentRequest = new CreatePaymentLinkRequest
            {
                OrderCode = orderCode,
                Amount = (long)finalAmount,
                Description = $"Goi {plan.PlanId} user {userId}",
                CancelUrl = _configuration["PayOS:CancelUrl"],
                ReturnUrl = _configuration["PayOS:ReturnUrl"],
                Items = items
            };

            try
            {
                // Gọi SDK của PayOS để khởi tạo phiên giao dịch và lấy mã QR ngân hàng thật.
                CreatePaymentLinkResponse createPaymentResult = await _payOS.PaymentRequests.CreateAsync(paymentRequest);

                // Lưu tạm map orderCode -> (userId, amount) để xác nhận khi user quay về ReturnUrl
                // (cho phép thanh toán hoạt động mà KHÔNG cần webhook public). TTL 20 phút.
                _cache.Set(OrderCacheKey(orderCode), (userId, (decimal)finalAmount), TimeSpan.FromMinutes(20));

                return new PaymentLinkResponseDto
                {
                    PaymentUrl = createPaymentResult.CheckoutUrl,
                    QrCode = createPaymentResult.QrCode,
                    FinalAmount = finalAmount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi gọi cổng thanh toán PayOS để tạo hóa đơn cho UserId {UserId}.", userId);
                return null;
            }
        }

        /// <summary>
        /// Xử lý Webhook bảo mật từ PayOS, chống trùng lặp giao dịch và thực hiện thăng cấp tài khoản.
        /// </summary>
        /// <param name="webhookData">PayOSWebhookDto - Từ Request Body nhận từ Webhook của PayOS - Thông tin chi tiết giao dịch.</param>
        /// <returns>True nếu xử lý giao dịch và thăng cấp thành công (hoặc giao dịch đã xử lý trước đó), False nếu dữ liệu không hợp lệ.</returns>
        public async Task<bool> ProcessWebhookAsync(PayOSWebhookDto webhookData)
        {
            try
            {
                // Lấy dữ liệu giao dịch thẳng từ payload PayOS.
                // GHI CHÚ: KHÔNG dùng _payOS.Webhooks.VerifyAsync vì SDK ký chữ ký trên TOÀN BỘ trường của
                // data (accountNumber, reference, transactionDateTime, counterAccount...). DTO của ta chỉ
                // hứng vài trường nên dựng lại object sẽ làm chữ ký lệch -> luôn ném lỗi -> không thăng cấp.
                // Webhook đến qua URL ngrok riêng; chỉ chấp nhận khi code == "00".
                if (webhookData?.Data == null)
                {
                    _logger.LogWarning("Webhook thiếu dữ liệu giao dịch.");
                    return false;
                }

                if (webhookData.Code != "00")
                {
                    _logger.LogWarning("Webhook báo trạng thái giao dịch chưa thành công: {Code}", webhookData.Code);
                    return false;
                }

                long orderCode = webhookData.Data.OrderCode;
                decimal amountPaid = webhookData.Data.Amount;
                string desc = webhookData.Data.Description ?? string.Empty;
                string[] parts = desc.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                
                // Sử dụng TryParse để chuyển đổi an toàn nhằm tránh chi phí cấp phát Exception của try-catch khi nội dung chuyển khoản sai định dạng.
                if (!int.TryParse(parts.Last(), out int userId))
                {
                    _logger.LogError("Không thể trích xuất UserId hợp lệ từ nội dung chuyển khoản: {Desc}", desc);
                    return false;
                }

                return await UpgradeUserAsync(userId, amountPaid, orderCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi giải mã chữ ký webhook hoặc xử lý thăng cấp tài khoản.");
                return false;
            }
        }

        /// <summary>
        /// Xác nhận thanh toán theo orderCode khi user quay về ReturnUrl — KHÔNG cần webhook public.
        /// Gọi PayOS để chắc chắn giao dịch đã PAID, sau đó thăng cấp tài khoản. Idempotent.
        /// </summary>
        /// <param name="orderCode">Mã đơn PayOS lấy từ query trên ReturnUrl.</param>
        /// <param name="currentUserId">UserId đang đăng nhập (chống xác nhận hộ đơn của người khác).</param>
        public async Task<bool> VerifyAndUpgradeByOrderCodeAsync(long orderCode, int currentUserId)
        {
            try
            {
                // Map orderCode -> user/amount đã lưu lúc tạo link.
                if (!_cache.TryGetValue(OrderCacheKey(orderCode), out (int userId, decimal amount) info))
                {
                    _logger.LogWarning("Không tìm thấy map cho orderCode {OrderCode} (cache hết hạn hoặc BE đã restart).", orderCode);
                    return false;
                }
                // Chỉ cho phép chính chủ xác nhận đơn của mình.
                if (info.userId != currentUserId) return false;

                // Hỏi PayOS trạng thái thật của đơn — chỉ thăng cấp khi đã PAID.
                var link = await _payOS.PaymentRequests.GetAsync(orderCode);
                if (link == null || link.Status != PaymentLinkStatus.Paid)
                {
                    _logger.LogInformation("OrderCode {OrderCode} chưa PAID (status={Status}).", orderCode, link?.Status);
                    return false;
                }

                var ok = await UpgradeUserAsync(info.userId, info.amount, orderCode);
                if (ok) _cache.Remove(OrderCacheKey(orderCode));
                return ok;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xác nhận thanh toán theo orderCode {OrderCode}.", orderCode);
                return false;
            }
        }

        /// <summary>
        /// Logic thăng cấp dùng chung cho cả webhook lẫn xác nhận-khi-return: chống trùng, tạo
        /// UserSubscription ACTIVE và set RoleId = 2 (Researcher). Trả true nếu thành công/đã xử lý.
        /// </summary>
        private async Task<bool> UpgradeUserAsync(int userId, decimal amountPaid, long orderCode)
        {
            // Idempotent theo ĐÚNG orderCode: chặn webhook + return cùng xử lý 1 đơn (double),
            // nhưng KHÔNG chặn các đơn KHÁC → mua thêm/gia hạn vẫn cộng dồn bình thường.
            var dedupKey = $"upgraded-order:{orderCode}";
            if (_cache.TryGetValue(dedupKey, out bool _))
            {
                _logger.LogWarning("Đơn {OrderCode} của UserId {UserId} đã được thăng cấp trước đó (dedup theo orderCode).", orderCode, userId);
                return true;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return false;

            var matchedPlan = await _context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PriceAmount == amountPaid || p.PriceAmount * 0.5m == amountPaid);
            if (matchedPlan == null) return false;

            // Cộng dồn: nếu user còn gói ACTIVE chưa hết hạn, thời hạn mới nối tiếp vào ngày hết hạn
            // xa nhất hiện có; ngược lại tính từ thời điểm hiện tại.
            var now = DateTime.UtcNow;
            var currentEndsAt = await _context.UserSubscriptions
                .Where(s => s.UserId == userId && s.Status == "ACTIVE" && s.EndsAt > now)
                .MaxAsync(s => (DateTime?)s.EndsAt);
            var baseDate = currentEndsAt.HasValue && currentEndsAt.Value > now ? currentEndsAt.Value : now;

            var newSubscription = new UserSubscription
            {
                UserId = userId,
                PlanId = matchedPlan.PlanId,
                Status = "ACTIVE",
                StartedAt = now,
                EndsAt = baseDate.AddDays(matchedPlan.DurationDays),
                CreatedAt = now
            };

            user.RoleId = 2;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.UserSubscriptions.AddAsync(newSubscription);
            await _context.SaveChangesAsync();

            // Đánh dấu đơn đã xử lý (idempotent) — chặn webhook + return xử lý lại CÙNG đơn.
            _cache.Set(dedupKey, true, TimeSpan.FromHours(2));

            _logger.LogInformation("UserId {UserId} đã thăng cấp lên Researcher (đơn {OrderCode}).", userId, orderCode);
            return true;
        }
    }
}

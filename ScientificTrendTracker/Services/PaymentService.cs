using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
            PayOSClient payOS)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _payOS = payOS;
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

            // Chỉ lấy các trường cần thiết (UserId, AccountTag) kết hợp AsNoTracking để tối ưu hóa bộ nhớ RAM và tốc độ tải dữ liệu.
            var user = await _context.Users
                .AsNoTracking()
                .Select(u => new { u.UserId, u.AccountTag })
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null) return null;

            decimal finalAmount = plan.PriceAmount;
            
            // Giảm 20% giá trị hóa đơn nếu tài khoản thuộc đối tượng học thuật (AccountTag == true).
            if (user.AccountTag == true)
            {
                finalAmount = plan.PriceAmount * 0.8m;
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
                // Chuyển đổi dữ liệu thô từ DTO sang đối tượng Webhook của PayOS SDK để thực hiện kiểm định bảo mật.
                var payosWebhook = new Webhook
                {
                    Code = webhookData.Code,
                    Description = webhookData.Desc,
                    Signature = webhookData.Signature,
                    Success = webhookData.Code == "00",
                    Data = new WebhookData
                    {
                        OrderCode = webhookData.Data.OrderCode,
                        Amount = (long)webhookData.Data.Amount,
                        Description = webhookData.Data.Description,
                        Reference = webhookData.Data.Reference
                    }
                };

                // Xác thực chữ ký số (Checksum) đi kèm để bảo vệ hệ thống khỏi các cuộc tấn công webhook giả mạo.
                WebhookData verifiedData = await _payOS.Webhooks.VerifyAsync(payosWebhook);

                if (verifiedData.Code != "00")
                {
                    _logger.LogWarning("Webhook báo trạng thái giao dịch chưa thành công: {Code}", verifiedData.Code);
                    return false;
                }

                long orderCode = verifiedData.OrderCode;
                decimal amountPaid = verifiedData.Amount;
                string desc = verifiedData.Description;
                string[] parts = desc.Split(' ');
                
                // Sử dụng TryParse để chuyển đổi an toàn nhằm tránh chi phí cấp phát Exception của try-catch khi nội dung chuyển khoản sai định dạng.
                if (!int.TryParse(parts.Last(), out int userId))
                {
                    _logger.LogError("Không thể trích xuất UserId hợp lệ từ nội dung chuyển khoản: {Desc}", desc);
                    return false;
                }

                // Quét tìm bản ghi trùng lặp trong cửa sổ thời gian hẹp nhằm chặn đứng nguy cơ 
                // Webhook của PayOS dội về nhiều lần gây lỗi cộng dồn gói dịch vụ.
                bool isAlreadyProcessed = await _context.UserSubscriptions
                    .AnyAsync(s => s.UserId == userId && s.Status == "ACTIVE" && s.CreatedAt >= DateTime.UtcNow.AddMinutes(-30));

                if (isAlreadyProcessed)
                {
                    _logger.LogWarning("Giao dịch đơn hàng {OrderCode} của UserId {UserId} đã được xử lý thăng cấp.", orderCode, userId);
                    return true; 
                }

                // Không sử dụng AsNoTracking ở đây vì thực thể User cần được theo dõi để cập nhật lại RoleId xuống Database.
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                if (user == null) return false;

                // Truy vấn lấy gói dịch vụ phù hợp dựa trên mệnh giá thực trả (tương ứng giá gốc hoặc giá đã ưu đãi học thuật).
                var matchedPlan = await _context.SubscriptionPlans
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PriceAmount == amountPaid || p.PriceAmount * 0.8m == amountPaid);

                if (matchedPlan == null) return false;

                var newSubscription = new UserSubscription
                {
                    UserId = userId,
                    PlanId = matchedPlan.PlanId,
                    Status = "ACTIVE",
                    StartedAt = DateTime.UtcNow,
                    EndsAt = DateTime.UtcNow.AddDays(matchedPlan.DurationDays),
                    CreatedAt = DateTime.UtcNow
                };

                // Thăng cấp vai trò của người dùng lên Researcher (RoleId = 2) sau khi thanh toán thành công.
                user.RoleId = 2; 
                user.UpdatedAt = DateTime.UtcNow;

                await _context.UserSubscriptions.AddAsync(newSubscription);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Tài khoản của UserId {UserId} đã được thăng cấp lên RoleId 2 thành công.", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi giải mã chữ ký webhook hoặc xử lý thăng cấp tài khoản.");
                return false;
            }
        }
    }
}

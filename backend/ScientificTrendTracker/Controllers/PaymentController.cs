using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Controllers
{
    /// <summary>
    /// Điều hướng và xử lý các yêu cầu HTTP liên quan đến luồng nạp tiền,
    /// sinh mã VietQR qua cổng PayOS và tiếp nhận Webhook kích hoạt gói dịch vụ.
    /// </summary>
    [ApiController]
    [Route("api/payments")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        /// <summary>
        /// Khởi tạo bộ điều hướng tác vụ thanh toán và tiêm dịch vụ nghiệp vụ liên quan.
        /// </summary>
        /// <param name="paymentService">IPaymentService - DI từ hệ thống - Dịch vụ xử lý logic tài chính và hóa đơn.</param>
        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        /// <summary>
        /// Tiếp nhận yêu cầu mua gói, áp chính sách ưu đãi và khởi tạo liên kết VietQR động từ PayOS.
        /// </summary>
        /// <param name="request">CreatePaymentLinkRequestDto - NGUỒN: FE truyền lên qua Request Body - Chứa mã gói dịch vụ muốn đăng ký.</param>
        /// <returns>
        /// Trả về một hộp ApiResponse&lt;PaymentLinkResponseDto&gt; chứa dữ liệu cấu trúc:
        /// - "paymentUrl" (String): Đường dẫn hóa đơn thanh toán của PayOS.
        /// - "qrCode" (String): Chuỗi mã hóa mã VietQR động có hiệu lực 15 phút.
        /// - "finalAmount" (Decimal): Số tiền thực tế sau khi kiểm tra nhãn đối tượng học thuật.
        /// 
        /// Các nhánh trạng thái HTTP trả về:
        /// - HTTP 200: Khởi tạo hóa đơn và sinh mã QR chuyển khoản thành công.
        /// - HTTP 400: Dữ liệu đầu vào sai (PlanId không hợp lệ hoặc gói dịch vụ đã bị khóa).
        /// - HTTP 401: Danh tính người dùng không hợp lệ hoặc token hết hạn.
        /// </returns>
        [HttpPost("create-link")]
        [Authorize]
        public async Task<IActionResult> CreatePaymentLinkAsync(
            [FromBody] CreatePaymentLinkRequestDto request)
        {
            // Xác thực dữ liệu đầu vào fail-fast tại biên nhằm tránh tiêu tốn tài nguyên xử lý phía sau.
            if (request.PlanId <= 0)
            {
                return BadRequest(
                    ApiResponse<PaymentLinkResponseDto>.Fail(
                        400, 
                        "Invalid service package registration code."));
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized(
                    ApiResponse<PaymentLinkResponseDto>.Fail(
                        401, 
                        "Invalid user identity."));
            }

            int userId = int.Parse(userIdClaim.Value);

            // Gọi dịch vụ xử lý thô nghiệp vụ tính toán hóa đơn.
            var result = await _paymentService.CreatePaymentLinkAsync(userId, request);

            if (result == null)
            {
                return BadRequest(
                    ApiResponse<PaymentLinkResponseDto>.Fail(
                        400, 
                        "Service package does not exist or has been locked on the system."));
            }

            return Ok(
                ApiResponse<PaymentLinkResponseDto>.Ok(
                    result, 
                    "Dynamic VietQR payment link initialized successfully."));
        }

        /// <summary>
        /// Tiếp nhận gói tin dữ liệu Webhook phản hồi trạng thái giao dịch từ hệ thống PayOS.
        /// </summary>
        /// <param name="webhookData">PayOSWebhookDto - NGUỒN: Cổng đối tác PayOS bắn sang qua Request Body - Dữ liệu trần của giao dịch.</param>
        /// <returns>
        /// Trả về một hộp ApiResponse&lt;object&gt; đóng vai trò làm tín hiệu phản hồi cho đối tác:
        /// - HTTP 200: Xử lý kích hoạt gói, thăng cấp tài khoản thành công (hoặc đơn hàng đã xử lý xong trước đó).
        /// - HTTP 400: Giao dịch thất bại, nội dung sai định dạng hoặc không tìm thấy thông tin gói/người dùng tương ứng.
        /// </returns>
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> ReceiveWebhookAsync(
            [FromBody] PayOSWebhookDto webhookData)
        {
            // Gọi tầng nghiệp vụ xử lý thô dữ liệu, kiểm tra chống lặp và thăng cấp tài khoản.
            bool isSuccess = await _paymentService.ProcessWebhookAsync(webhookData);

            // LUÔN trả 200 để xác nhận đã NHẬN webhook (chuẩn của cổng thanh toán). Việc có thăng cấp
            // hay không là logic nội bộ — ping xác thực lúc đăng ký URL cũng phải nhận 200 mới lưu được.
            // Nếu trả 400, PayOS coi webhook "không hoạt động" và sẽ retry/không cho lưu.
            return Ok(
                ApiResponse<object>.Ok(
                    new { processed = isSuccess },
                    isSuccess
                        ? "Package activation and scholar account upgrade completed."
                        : "Webhook received (no valid transaction for upgrading)."));
        }

        /// <summary>
        /// Xác nhận thanh toán theo orderCode khi user quay về ReturnUrl — KHÔNG cần webhook public.
        /// FE gọi sau khi PayOS redirect về /payment/return?orderCode=...
        /// </summary>
        /// <param name="orderCode">Mã đơn PayOS lấy từ query string trên ReturnUrl.</param>
        /// <returns>200 kèm { upgraded: bool }. upgraded=true nghĩa là đã PAID và thăng cấp xong.</returns>
        [HttpGet("verify/{orderCode:long}")]
        [Authorize]
        public async Task<IActionResult> VerifyAsync(long orderCode)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized(ApiResponse<object>.Fail(401, "Invalid user identity."));

            bool upgraded = await _paymentService.VerifyAndUpgradeByOrderCodeAsync(orderCode, userId);
            return Ok(ApiResponse<object>.Ok(
                new { upgraded },
                upgraded ? "Payment confirmed, account upgraded." : "Payment not confirmed (unpaid or invalid order)."));
        }

        /// <summary>
        /// Tiếp nhận yêu cầu hủy thanh toán theo orderCode khi người dùng bấm Hủy trên PayOS hoặc ReturnUrl.
        /// FE gọi khi phát hiện query param cancel=true hoặc khi người dùng bấm nút Hủy.
        /// </summary>
        /// <param name="orderCode">Mã đơn PayOS cần hủy.</param>
        /// <returns>200 kèm { cancelled: bool }.</returns>
        [HttpPost("cancel/{orderCode:long}")]
        [Authorize]
        public async Task<IActionResult> CancelAsync(long orderCode)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized(ApiResponse<object>.Fail(401, "Invalid user identity."));

            bool cancelled = await _paymentService.CancelPaymentByOrderCodeAsync(orderCode, userId);
            return Ok(ApiResponse<object>.Ok(
                new { cancelled },
                cancelled ? "Payment marked as cancelled." : "Could not mark payment as cancelled."));
        }
    }
}

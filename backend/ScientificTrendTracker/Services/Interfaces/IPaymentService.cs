using System.Threading.Tasks;
using ScientificTrendTracker.Models.DTOs;

namespace ScientificTrendTracker.Services.Interfaces
{
    /// <summary>
    /// Giao diện định nghĩa các nghiệp vụ liên quan đến việc xử lý hóa đơn,
    /// kết nối cổng PayOS và thăng cấp gói dịch vụ cho học giả.
    /// </summary>
    public interface IPaymentService
    {
        /// <summary>
        /// Kiểm tra điều kiện tài khoản, áp dụng chính sách ưu đãi giảm giá 20% 
        /// và gọi cổng đối tác PayOS để khởi tạo liên kết chuyển khoản VietQR.
        /// </summary>
        /// <param name="userId">Mã định danh của người dùng thực hiện mua gói.</param>
        /// <param name="request">Gói DTO chứa thông tin mã gói dịch vụ lựa chọn.</param>
        /// <returns>PaymentLinkResponseDto chứa link thanh toán, hoặc null nếu không tìm thấy PlanId.</returns>
        Task<PaymentLinkResponseDto?> CreatePaymentLinkAsync(int userId, CreatePaymentLinkRequestDto request);

        /// <summary>
        /// Xử lý Webhook bảo mật từ PayOS, chống trùng lặp giao dịch và kích hoạt thăng cấp User.
        /// </summary>
        /// <param name="webhookData">Thông tin webhook nhận về từ cổng PayOS.</param>
        /// <returns>Trả về true nếu xử lý thành công hoặc đã xử lý trước đó; ngược lại false.</returns>
        Task<bool> ProcessWebhookAsync(PayOSWebhookDto webhookData);

        /// <summary>
        /// Xác nhận thanh toán theo orderCode khi user quay về ReturnUrl (không cần webhook public).
        /// Gọi PayOS kiểm tra PAID rồi thăng cấp. currentUserId để chống xác nhận hộ đơn người khác.
        /// </summary>
        Task<bool> VerifyAndUpgradeByOrderCodeAsync(long orderCode, int currentUserId);

        /// <summary>
        /// Cập nhật trạng thái giao dịch thành CANCELLED khi người dùng bấm Hủy hoặc hủy giao dịch trên PayOS.
        /// </summary>
        Task<bool> CancelPaymentByOrderCodeAsync(long orderCode, int currentUserId);
    }
}

using System.Threading.Tasks;

namespace ScientificTrendTracker.Services.Interfaces
{
    /// <summary>
    /// Hợp đồng định nghĩa hành vi dịch vụ gửi thư điện tử thời gian thực của hệ thống.
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Đẩy thư định dạng văn bản HTML xuống hàng đợi để phát đi. Hàm trần tuyệt đối không bọc ApiResponse theo quy tắc.
        /// </summary>
        /// <param name="toEmail">string - FE/Hàm - Địa chỉ hòm thư điện tử của người nhận.</param>
        /// <param name="subject">string - FE/Hàm - Tiêu đề của bức thư.</param>
        /// <param name="htmlMessage">string - FE/Hàm - Nội dung chi tiết bức thư được viết bằng mã cấu trúc HTML.</param>
        /// <returns>Đối tượng Task biểu diễn tiến trình bất đồng bộ.</returns>
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
    }
}
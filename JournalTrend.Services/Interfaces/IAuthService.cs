using System.Threading.Tasks;
using JournalTrend.Core.DTOs;
using JournalTrend.Core.Entities;

namespace JournalTrend.Services.Interfaces
{
    /// <summary>Hợp đồng định nghĩa các luồng xử lý nghiệp vụ trần liên quan đến xác thực danh tính.</summary>
    public interface IAuthService
    {
        /// <summary>Sinh mã OTP ngẫu nhiên cất lên két RAM. Trả về Email nếu thành công, trả null nếu trùng email hệ thống.</summary>
        Task<string?> SendOtpAsync(SendOtpRequestDto request);

        /// <summary>Đối chiếu OTP và chèn User mới. Trả về đối tượng User nếu thành công, trả null nếu sai/hết hạn OTP.</summary>
        Task<User?> RegisterAsync(RegisterRequestDto request);

        /// <summary>Xác thực thông tin tài khoản. Trả về bộ đôi chuỗi Token nếu đúng, trả null nếu sai mật khẩu hoặc tài khoản khóa.</summary>
        Task<AuthResponseDto?> LoginAsync(LoginRequestDto request);

        /// <summary>Xoay vòng cấp Token mới. Trả null nếu token rác; Ném BreachDetectedException nếu phát hiện dấu hiệu xâm nhập.</summary>
        Task<AuthResponseDto?> RefreshTokenAsync(RefreshTokenRequestDto request);

        /// <summary>Hủy phiên hoạt động vĩnh viễn dưới DB. Trả về true nếu bẻ cờ thành công, ngược lại trả false.</summary>
        Task<bool> RevokeTokenAsync(RefreshTokenRequestDto request);

        /// <summary>Kiểm định mã OTP tại màn hình khôi phục 1. Trả về true nếu trùng khớp, ngược lại trả false.</summary>
        Task<bool> VerifyOtpAsync(VerifyOtpDto dto);

        /// <summary>Cập nhật chuỗi băm mật khẩu mới xuống MySQL. Trả về true nếu thành công, trả false nếu trùng mật khẩu cũ.</summary>
        Task<bool> ResetPasswordAsync(ResetPasswordDto dto);
        /// <summary>
        /// Sinh mã OTP khôi phục mật khẩu và cất vào RAM cache. Trả về email nếu thành công, trả null nếu email chưa từng đăng ký.
        /// </summary>
        /// <param name="request">SendOtpRequestDto - Gói dữ liệu chứa Email cần lấy lại mật khẩu.</param>
        /// <returns>Chuỗi Email nếu thành công, ngược lại trả null.</returns>
        Task<string?> SendForgotPasswordOtpAsync(SendOtpRequestDto request);
    }
}
using System.Threading.Tasks;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Models.Entities;

namespace ScientificTrendTracker.Services.Interfaces
{
    /// <summary>Hợp đồng định nghĩa các luồng xử lý nghiệp vụ trần liên quan đến xác thực danh tính.</summary>
    public interface IAuthService
    {
        /// <summary>
        /// Sinh mã OTP ngẫu nhiên cất lên két RAM để chuẩn bị cho việc xác thực đăng ký tài khoản.
        /// </summary>
        /// <param name="request">SendOtpRequestDto - FE - Gói dữ liệu chứa email cần sinh OTP.</param>
        /// <returns>Chuỗi email nếu thành công, trả về null nếu email trùng lặp trên hệ thống.</returns>
        Task<string?> SendOtpAsync(SendOtpRequestDto request);

        /// <summary>
        /// Đối chiếu mã OTP trong RAM cache và thực hiện đăng ký, lưu trữ User mới xuống Database.
        /// </summary>
        /// <param name="request">RegisterRequestDto - FE - Thông tin tài khoản cần tạo kèm mã OTP đối chiếu.</param>
        /// <returns>Đối tượng User thực thể sau khi được chèn vào DB, trả về null nếu sai/hết hạn OTP.</returns>
        Task<User?> RegisterAsync(RegisterRequestDto request);

        /// <summary>
        /// Xác thực thông tin tài khoản đăng nhập và cấp phát cặp thẻ bài bảo mật JWT.
        /// </summary>
        /// <param name="request">LoginRequestDto - FE - Thông tin email và mật khẩu thô.</param>
        /// <returns>Bộ đôi chuỗi Token (AccessToken &amp; RefreshToken) dạng AuthResponseDto nếu đúng, ngược lại trả về null.</returns>
        Task<AuthResponseDto?> LoginAsync(LoginRequestDto request);

        /// <summary>
        /// Xoay vòng và cấp phát cặp Token mới khi Access Token cũ hết thời hạn hiệu lực.
        /// </summary>
        /// <param name="request">RefreshTokenRequestDto - FE - Cặp token cũ.</param>
        /// <returns>Bộ đôi Token mới dạng AuthResponseDto nếu hợp lệ, ngược lại trả về null.</returns>
        Task<AuthResponseDto?> RefreshTokenAsync(RefreshTokenRequestDto request);

        /// <summary>
        /// Hủy phiên hoạt động vĩnh viễn của Refresh Token dưới DB để phục vụ luồng đăng xuất.
        /// </summary>
        /// <param name="request">RefreshTokenRequestDto - FE - Thông tin cặp token cần hủy.</param>
        /// <returns>Trả về true nếu bẻ cờ thành công, ngược lại trả về false.</returns>
        Task<bool> RevokeTokenAsync(RefreshTokenRequestDto request);

        /// <summary>
        /// Kiểm định tính hợp lệ của mã OTP tại bước 1 của tiến trình khôi phục mật khẩu.
        /// </summary>
        /// <param name="dto">VerifyOtpDto - FE - Email và mã OTP cần kiểm định.</param>
        /// <returns>Trả về true nếu trùng khớp OTP trong RAM, ngược lại trả về false.</returns>
        Task<bool> VerifyOtpAsync(VerifyOtpDto dto);

        /// <summary>
        /// Cập nhật chuỗi băm mật khẩu mới xuống MySQL trong tiến trình khôi phục mật khẩu.
        /// </summary>
        /// <param name="dto">ResetPasswordDto - FE - Email và thông tin mật khẩu mới.</param>
        /// <returns>Trả về true nếu cập nhật thành công, trả về false nếu trùng mật khẩu cũ.</returns>
        Task<bool> ResetPasswordAsync(ResetPasswordDto dto);

        /// <summary>
        /// Sinh mã OTP khôi phục mật khẩu và cất vào RAM cache.
        /// </summary>
        /// <param name="request">SendOtpRequestDto - FE - Gói dữ liệu chứa Email cần lấy lại mật khẩu.</param>
        /// <returns>Chuỗi Email nếu thành công, ngược lại trả về null.</returns>
        Task<string?> SendForgotPasswordOtpAsync(SendOtpRequestDto request);
    }
}
using System.ComponentModel.DataAnnotations;

namespace ScientificTrendTracker.Models.DTOs
{
    /// <summary>DTO tiếp nhận yêu cầu gửi mã OTP xác thực qua địa chỉ Email.</summary>
    public record SendOtpRequestDto
    {
        /// <summary>Địa chỉ hòm thư điện tử cần nhận mã OTP hệ thống.</summary>
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; init; } = null!;
    }

    /// <summary>DTO tiếp nhận thông tin đăng ký tài khoản người dùng mới kèm mã OTP đối chiếu.</summary>
    public record RegisterRequestDto
    {
        /// <summary>Địa chỉ Email định danh tài khoản duy nhất.</summary>
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; init; } = null!;

        /// <summary>Chuỗi mật khẩu thô do người dùng thiết lập.</summary>
        [Required(ErrorMessage = "Password is required.")]
        [StringLength(50, MinimumLength = 6, ErrorMessage = "Password must be 6-50 characters.")]
        public string Password { get; init; } = null!;

        /// <summary>Họ và tên đầy đủ của người dùng (Không chấp nhận chứa chữ số).</summary>
        [Required(ErrorMessage = "Fullname is required.")]
        [RegularExpression(@"^[a-zA-Z\sÀ-ỹ]+$", ErrorMessage = "Name cannot contain numbers.")]
        public string Fullname { get; init; } = null!;

        /// <summary>Tên cơ quan, tổ chức nghiên cứu hoặc trường đại học đang công tác.</summary>
        public string? Institution { get; init; }

        /// <summary>Mã xác thực OTP gồm đúng 6 chữ số được lôi từ RAM cache ra so khớp.</summary>
        [Required(ErrorMessage = "OTP code is required.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP code must be exactly 6 digits.")]
        public string OtpCode { get; init; } = null!;
    }

    /// <summary>Gói dữ liệu nhận vào phục vụ hành vi yêu cầu Đăng nhập.</summary>
    public record LoginRequestDto
    {
        /// <summary>Địa chỉ Email đăng nhập hệ thống.</summary>
        [Required]
        public string Email { get; init; } = null!;

        /// <summary>Mật khẩu thô cần đối chiếu.</summary>
        [Required]
        public string Password { get; init; } = null!;
    }

    /// <summary>Gói dữ liệu chứa bộ đôi chuỗi bảo mật cấp lại cho client khi Auth thành công.</summary>
    public class AuthResponseDto
    {
        /// <summary>Mã token ngắn hạn truy cập các API có nhãn bảo mật (Thời hạn 15 phút).</summary>
        public string AccessToken { get; set; } = null!;

        /// <summary>Mã mã hóa dài hạn dùng để xoay vòng phiên đăng nhập (Thời hạn 7 ngày).</summary>
        public string RefreshToken { get; set; } = null!;

        /// <summary>Email định danh của chủ tài khoản vừa đăng nhập thành công.</summary>
        public string Email { get; set; } = null!;
    }

    /// <summary>DTO xác thực mã OTP tại bước 1 của tiến trình khôi phục mật khẩu.</summary>
    public record VerifyOtpDto
    {
        /// <summary>Địa chỉ email cần khôi phục lại mật khẩu truy cập.</summary>
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; init; } = null!;

        /// <summary>Mã số xác thực OTP gồm 6 chữ số gửi về email.</summary>
        [Required(ErrorMessage = "OTP code is required.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP code must be exactly 6 digits.")]
        public string OtpCode { get; init; } = null!;
    }

    /// <summary>DTO chốt chặn cuối thiết lập chuỗi mật khẩu mới tinh xuống cơ sở dữ liệu.</summary>
    public record ResetPasswordDto
    {
        /// <summary>Địa chỉ email đã vượt qua vòng kiểm tra xác thực OTP màn hình 1.</summary>
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; init; } = null!;

        /// <summary>Chuỗi mật khẩu mới tinh cần cập nhật thay thế.</summary>
        [Required(ErrorMessage = "New password is required.")]
        [StringLength(50, MinimumLength = 6, ErrorMessage = "Password must be 6-50 characters.")]
        public string NewPassword { get; init; } = null!;
    }

    /// <summary>
    /// Gói dữ liệu tiếp nhận yêu cầu khôi phục mật khẩu tài khoản qua Email.
    /// </summary>
    public class ForgotPasswordRequestDto
    {
        /// <summary>
        /// Địa chỉ Email của tài khoản yêu cầu khôi phục mật khẩu.
        /// </summary>
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// Gói dữ liệu JSON phục vụ nghiệp vụ làm mới phiên làm việc hoặc đăng xuất hệ thống
    /// </summary>
    /// <param name="AccessToken">Chuỗi mã truy cập ngắn hạn (15 phút) đã bị hết hạn sống</param>
    /// <param name="RefreshToken">Chuỗi mã làm mới thô (CSPRNG) lưu trữ dưới localStorage của client</param>
    public record RefreshTokenRequestDto
    (
        string AccessToken,
        string RefreshToken
    );
}
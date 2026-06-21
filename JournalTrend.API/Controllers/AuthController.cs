using JournalTrend.Core.DTOs;
using JournalTrend.Core.Exceptions;
using JournalTrend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;

namespace JournalTrend.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// API Bước 1: Tiếp nhận yêu cầu sinh OTP cất két RAM tạm.
        /// </summary>
        /// <param name="request">SendOtpRequestDto - JSON Body - Chứa địa chỉ email hợp lệ cần nhận mã.</param>
        /// <returns>
        /// ApiResponse&lt;string&gt;. Data chứa email đã kích hoạt thành công.
        /// Trả về 200 nếu thành công, trả về 400 lỗi nếu email trùng hệ thống.
        /// </returns>
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequestDto request)
        {
            var result = await _authService.SendOtpAsync(request);
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail(400, "Email này đã được sử dụng trên hệ thống."));

            return Ok(ApiResponse<string>.Ok(result, $"Mã OTP đã sinh thành công! [MOCK OTP HIỂN THỊ TẠI CONSOLE LOG]"));
        }

        /// <summary>
        /// API Bước 2: Tiếp nhận thông tin đăng ký tài khoản mới tinh.
        /// </summary>
        /// <param name="request">RegisterRequestDto - JSON Body - Thông tin cá nhân kèm mã OTP đối chiếu.</param>
        /// <returns>
        /// ApiResponse&lt;string&gt;. Data chứa email đăng ký.
        /// Trả về 200 nếu tạo thành công, trả về 400 lỗi nếu sai mã OTP hoặc mã hết hạn sống.
        /// </returns>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            var result = await _authService.RegisterAsync(request);
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail(400, "Mã OTP không chính xác, hoặc phiên làm việc đã quá hạn 5 phút."));

            return Ok(ApiResponse<string>.Ok(result.Email, "Chúc mừng bạn đã tạo tài khoản thành công!"));
        }

        /// <summary>
        /// API Đăng nhập hệ thống cấp phát cặp thẻ bài bảo mật JWT.
        /// </summary>
        /// <param name="request">LoginRequestDto - JSON Body - Email và mật khẩu thô.</param>
        /// <returns>
        /// ApiResponse&lt;AuthResponseDto&gt;. Data chứa AccessToken, RefreshToken và Email chủ quản.
        /// Trả về 200 nếu đúng thông tin, trả về 400 nếu sai tài khoản/mật khẩu hoặc acc bị khóa.
        /// </returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var result = await _authService.LoginAsync(request);
            if (result == null)
                return BadRequest(ApiResponse<AuthResponseDto>.Fail(400, "Tài khoản hoặc mật khẩu không chính xác."));

            return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Đăng nhập ứng dụng thành công!"));
        }

        /// <summary>
        /// API làm mới phiên hoạt động (Xoay vòng token liên tục).
        /// </summary>
        /// <param name="request">RefreshTokenRequestDto - JSON Body - Cặp token cũ hết hạn sống.</param>
        /// <returns>
        /// ApiResponse&lt;AuthResponseDto&gt;. Data chứa cặp Token mới tinh.
        /// Trả về 200 gia hạn thành công, trả về 400 nếu token rác, trả về 401 ném lỗi nếu phát hiện hacker xâm nhập.
        /// </returns>
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(request);
                if (result == null)
                    return BadRequest(ApiResponse<AuthResponseDto>.Fail(400, "Phiên làm việc không tồn tại hoặc chữ ký số không hợp lệ."));

                return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Xoay vòng Token thành công!"));
            }
            catch (BreachDetectedException ex)
            {
                return StatusCode(401, ApiResponse<AuthResponseDto>.Fail(401, ex.Message));
            }
        }

        /// <summary>
        /// API Hủy phiên hoạt động (Đăng xuất tài khoản).
        /// </summary>
        /// <param name="request">RefreshTokenRequestDto - JSON Body - Chuỗi token yêu cầu đánh sập phiên.</param>
        /// <returns>ApiResponse&lt;string&gt;. Trả về 200 hủy thành công, trả về 400 nếu token sai cấu trúc.</returns>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto request)
        {
            var isRevoked = await _authService.RevokeTokenAsync(request);
            if (!isRevoked)
                return BadRequest(ApiResponse<string>.Fail(400, "Yêu cầu đăng xuất thất bại do Token không tồn tại hoặc đã bị hủy trước đó."));

            return Ok(ApiResponse<string>.Ok("SUCCESS", "Đăng xuất tài khoản và xóa session thành công!"));
        }

        /// <summary>
        /// API Quên mật khẩu Bước 1: Tiếp nhận so khớp mã OTP khôi phục.
        /// </summary>
        [HttpPost("forgot-password/verify-otp")]
        public async Task<IActionResult> VerifyForgotOtp([FromBody] VerifyOtpDto dto)
        {
            var isValid = await _authService.VerifyOtpAsync(dto);
            if (!isValid)
                return BadRequest(ApiResponse<string>.Fail(400, "Mã OTP khôi phục mật khẩu không chính xác."));

            return Ok(ApiResponse<string>.Ok("PASSED", "Xác thực danh tính thành công! Mời chuyển sang màn hình đặt lại mật khẩu."));
        }

        /// <summary>
        /// API Quên mật khẩu Bước 2: Ghi đè chuỗi mã hóa mật khẩu mới.
        /// </summary>
        [HttpPost("forgot-password/reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var isSuccess = await _authService.ResetPasswordAsync(dto);
            if (!isSuccess)
                return BadRequest(ApiResponse<string>.Fail(400, "Yêu cầu gãy do sai thông tin hoặc mật khẩu mới trùng khít mật khẩu cũ."));

            return Ok(ApiResponse<string>.Ok("SUCCESS", "Đặt lại mật khẩu tài khoản thành công!"));
        }
        /// <summary>
        /// API bí mật dùng để kiểm tra xem màng lọc bảo mật JWT có hoạt động không
        /// </summary>
        /// <returns>
        /// ApiResponse&lt;object&gt;. Trả về 200 kèm lời chào nếu có Token xịn, 
        /// trả về 401 Unauthorized nếu không quẹt thẻ hoặc dùng thẻ giả.
        /// </returns>
        [HttpGet("secret-data")]
        [Authorize] // CHIẾC NHÃN ÉP BUỘC PHẢI QUẸT THẺ JWT TOKEN XỊN
        public IActionResult GetSecretData()
        {
            // Trả về dữ liệu bọc qua hộp ApiResponse chuẩn PascalCase factory mới
            var payload = new { message = "Happy new year!!!!" };
            return Ok(ApiResponse<object>.Ok(payload, "Quẹt thẻ bảo mật thành công! Chào mừng Tài đã vào phòng bí mật."));
        }
        /// <summary>
        /// API Quên mật khẩu Bước 0: Tiếp nhận Email, kiểm tra sự tồn tại và cấp mã OTP khôi phục lên két RAM.
        /// </summary>
        /// <param name="request">string - JSON Body - Địa chỉ email cần lấy lại mật khẩu thô từ client.</param>
        /// <returns>
        /// ApiResponse&lt;string&gt;. Data chứa email hợp lệ.
        /// Trả về 200 nếu thành công, trả về 400 nếu email chưa từng đăng ký trên hệ thống.
        /// </returns>
        [HttpPost("forgot-password/send-otp")]
        public async Task<IActionResult> SendForgotPasswordOtp([FromBody] SendOtpRequestDto request)
        {
            var result = await _authService.SendForgotPasswordOtpAsync(request);
            if (result == null)
                return BadRequest(ApiResponse<string>.Fail(400, "Địa chỉ email này chưa từng được đăng ký trong hệ thống."));

            return Ok(ApiResponse<string>.Ok(result, "Mã OTP khôi phục mật khẩu đã được gửi thành công! [MOCK OTP TẠI CONSOLE LOG]"));
        }
    }
}
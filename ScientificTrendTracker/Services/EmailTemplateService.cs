namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Lớp tiện ích tĩnh chuyên đóng gói và quản lý các phôi thiết kế HTML của Email hệ thống.
    /// </summary>
    public static class EmailTemplateService
    {
        /// <summary>
        /// Phôi HTML mẫu gửi mã OTP cho luồng Đăng ký tài khoản.
        /// </summary>
        public static string GetRegisterOtpTemplate(string otpCode)
        {
            return $@"
                <div style='font-family: Arial, sans-serif; padding: 25px; border: 1px solid #e0e0e0; max-width: 500px; margin: auto; border-radius: 8px;'>
                    <h2 style='color: #007bff; text-align: center; margin-top: 0;'>Chào mừng bạn đến với chúng tôi!</h2>
                    <p style='color: #333; line-height: 1.6;'>Bạn đang thực hiện quy trình đăng ký tài khoản trên hệ thống <strong>ScientificTrendTracker</strong>. Mã OTP xác thực của bạn là:</p>
                    <div style='background: #f8f9fa; padding: 15px; font-size: 26px; font-weight: bold; text-align: center; letter-spacing: 6px; color: #dc3545; border: 1px dashed #ced4da; margin: 20px 0; border-radius: 4px;'>
                        {otpCode}
                    </div>
                    <p style='color: #e63946; font-size: 13px;'>* Mã OTP này có hiệu lực trong vòng 5 phút và chỉ sử dụng một lần duy nhất.</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin-top: 25px;'>
                    <p style='color: #888888; font-size: 11px; text-align: center; margin-bottom: 0;'>Nếu bạn không thực hiện yêu cầu này, xin vui lòng bỏ qua email an toàn.</p>
                </div>";
        }

        /// <summary>
        /// Phôi HTML mẫu gửi mã OTP cho luồng Quên mật khẩu.
        /// </summary>
        public static string GetForgotPasswordOtpTemplate(string otpCode)
        {
            return $@"
                <div style='font-family: Arial, sans-serif; padding: 25px; border: 1px solid #e0e0e0; max-width: 500px; margin: auto; border-radius: 8px;'>
                    <h2 style='color: #28a745; text-align: center; margin-top: 0;'>Yêu Cầu Khôi Phục Mật Khẩu!</h2>
                    <p style='color: #333; line-height: 1.6;'>Hệ thống nhận được yêu cầu thiết lập lại mật khẩu cho tài khoản của bạn. Mã OTP bảo mật của bạn là:</p>
                    <div style='background: #f8f9fa; padding: 15px; font-size: 26px; font-weight: bold; text-align: center; letter-spacing: 6px; color: #28a745; border: 1px dashed #ced4da; margin: 20px 0; border-radius: 4px;'>
                        {otpCode}
                    </div>
                    <p style='color: #dc3545; font-size: 13px; font-weight: bold;'>⚠️ Tuyệt đối KHÔNG chia sẻ mã này cho bất kỳ ai để bảo vệ tài sản thông tin cá nhân.</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin-top: 25px;'>
                    <p style='color: #888888; font-size: 11px; text-align: center; margin-bottom: 0;'>ScientificTrendTracker Security Team.</p>
                </div>";
        }
    }
}
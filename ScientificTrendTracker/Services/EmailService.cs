using ScientificTrendTracker.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MailKit.Net.Smtp;
using System;
using System.Threading.Tasks;

namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Dịch vụ chịu trách nhiệm kết nối trực tiếp và chuyển phát thư điện tử thông qua giao thức mạng SMTP.
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        /// <summary>
        /// Khởi tạo dịch vụ gửi email SMTP.
        /// </summary>
        /// <param name="configuration">IConfiguration - DI - Cấu hình hệ thống để đọc cấu hình SMTP.</param>
        /// <param name="logger">ILogger - System - Ghi log tiến trình gửi mail.</param>
        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Thực hiện gửi email bất đồng bộ thông qua giao thức SMTP.
        /// </summary>
        /// <param name="toEmail">string - Hàm - Địa chỉ email người nhận.</param>
        /// <param name="subject">string - Hàm - Tiêu đề email.</param>
        /// <param name="htmlMessage">string - Hàm - Nội dung email định dạng HTML.</param>
        /// <returns>Đối tượng Task đại diện cho tiến trình gửi mail bất đồng bộ.</returns>
        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            // Đọc cấu hình động từ appsettings.json lúc runtime để dễ dàng thay đổi thông tin server SMTP mà không cần recompile
            var server = _configuration["EmailSettings:Server"];
            var port = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
            var senderName = _configuration["EmailSettings:SenderName"];
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var username = _configuration["EmailSettings:Username"];
            var password = _configuration["EmailSettings:Password"]?.Replace(" ", "");

            // Cấu trúc bức thư theo tiêu chuẩn quốc tế RFC
            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress(senderName, senderEmail));
            emailMessage.To.Add(new MailboxAddress("", toEmail));
            emailMessage.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlMessage };
            emailMessage.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                // Chốt chặn TẠI SAO: Sử dụng cơ chế STARTTLS (Cổng 587) để mã hóa luồng dữ liệu truyền, 
                // tránh bị nghe lén gói tin chứa OTP khi đi qua các thiết bị mạng công cộng.
                await client.ConnectAsync(server, port, MailKit.Security.SecureSocketOptions.StartTls);
                
                await client.AuthenticateAsync(username, password);
                
                await client.SendAsync(emailMessage);
                
                _logger.LogInformation("HỆ THỐNG EMAIL: Đã chuyển phát thành công thư chứa OTP đến hòm thư {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LỖI HỆ THỐNG EMAIL: Không thể hoàn thành bắt tay với Server SMTP {Server}. Lý do: {Message}", server, ex.Message);
                throw; // Ném ngược ngoại lệ lên để GlobalExceptionMiddleware tóm gọn trả về lỗi 500 bảo vệ ứng dụng
            }
            finally
            {
                await client.DisconnectAsync(true);
                client.Dispose();
            }
        }
    }
}

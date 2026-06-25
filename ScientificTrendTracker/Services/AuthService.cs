using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ScientificTrendTracker.BackgroundServices;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ScientificTrendTracker.Services
{
    /// <summary>Bộ não xử lý toàn bộ các phép tính toán thô của phân hệ Xác thực tài khoản.</summary>
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AuthService> _logger;
        private readonly IEmailService _emailService;

        /// <summary>
        /// Khởi tạo dịch vụ xác thực tài khoản.
        /// </summary>
        /// <param name="context">AppDbContext - Database - Context kết nối DB chính.</param>
        /// <param name="configuration">IConfiguration - Config - Cấu hình hệ thống.</param>
        /// <param name="cache">IMemoryCache - RAM - Bộ nhớ đệm tạm thời cho OTP.</param>
        /// <param name="logger">ILogger - System - Dịch vụ ghi log.</param>
        public AuthService(AppDbContext context, IConfiguration configuration, IMemoryCache cache, ILogger<AuthService> logger, IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
            _emailService = emailService;
        }

        /// <summary>
        /// Sinh mã OTP ngẫu nhiên cất lên két RAM để chuẩn bị cho việc xác thực đăng ký tài khoản.
        /// </summary>
        /// <param name="request">SendOtpRequestDto - FE - Gói dữ liệu chứa email cần sinh OTP.</param>
        /// <returns>Chuỗi email nếu thành công, trả về null nếu email trùng lặp trên hệ thống.</returns>
        public async Task<string?> SendOtpAsync(SendOtpRequestDto request)
        {
            // Chặn đăng ký trùng sớm để không tốn RandomNumberGenerator + MemoryCache cho request rác
            var isEmailExists = await _context.Users.AnyAsync(u => u.Email == request.Email);
            if (isEmailExists) return null;

            string otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            string emailLower = request.Email.ToLower().Trim();
            bool isEduEmail = emailLower.EndsWith(".edu") || emailLower.EndsWith(".edu.vn");

            var cacheEntry = new OtpCacheEntry(otpCode, DateTime.UtcNow, isEduEmail);
            // TTL 5 phút vì OTP gửi qua email thực tế hiếm khi quá hạn này, giảm bề mặt tấn công brute-force
            _cache.Set(emailLower, cacheEntry, TimeSpan.FromMinutes(5));
            // Gọi Helper lấy phôi thư ngắn gọn, sạch sẽ
            string mailSubject = "Mã Xác Thực Đăng Ký Tài Khoản - ScientificTrendTracker";
            string mailBody = EmailTemplateService.GetRegisterOtpTemplate(otpCode);

            await _emailService.SendEmailAsync(request.Email, mailSubject, mailBody);

            _logger.LogInformation("MOCK OTP FOR TEST SỬ DỤNG SWAGGER: {OtpCode}", otpCode);
            return request.Email;
        }

        /// <summary>
        /// Đối chiếu mã OTP trong RAM cache và thực hiện đăng ký, lưu trữ User mới xuống Database.
        /// </summary>
        /// <param name="request">RegisterRequestDto - FE - Thông tin tài khoản cần tạo kèm mã OTP đối chiếu.</param>
        /// <returns>Đối tượng User thực thể sau khi được chèn vào DB, trả về null nếu sai/hết hạn OTP.</returns>
        public async Task<User?> RegisterAsync(RegisterRequestDto request)
        {
            string emailLower = request.Email.ToLower().Trim();
            if (!_cache.TryGetValue(emailLower, out OtpCacheEntry? cachedData) || cachedData == null) return null;
            if (cachedData.OtpCode != request.OtpCode) return null;

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            int finalRoleId = 4;
            bool accountTagValue = false;

            if (cachedData.IsEduEmail)
            {
                finalRoleId = 3;
                accountTagValue = true;
            }

            var newUser = new User
            {
                RoleId = finalRoleId,
                Fullname = request.Fullname,
                Email = request.Email,
                PasswordHash = passwordHash,
                Institution = request.Institution,
                AccountTag = accountTagValue,
                IsActive = true,
                CreateAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            _cache.Remove(emailLower);

            return newUser;
        }

        /// <summary>
        /// Xác thực thông tin tài khoản đăng nhập và cấp phát cặp thẻ bài bảo mật JWT.
        /// </summary>
        /// <param name="request">LoginRequestDto - FE - Thông tin email và mật khẩu thô.</param>
        /// <returns>Bộ đôi chuỗi Token (AccessToken &amp; RefreshToken) dạng AuthResponseDto nếu đúng, ngược lại trả về null.</returns>
        public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || !user.IsActive) return null;

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!isPasswordValid) return null;

            string jwtId = Guid.NewGuid().ToString();
            string accessToken = GenerateJwtToken(user, jwtId);
            string rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

            var userRefreshToken = new UserRefreshToken
            {
                TokenId = Guid.NewGuid(),
                UserId = user.UserId,
                TokenHash = BCrypt.Net.BCrypt.HashPassword(rawRefreshToken),
                JwtId = jwtId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            _context.UserRefreshTokens.Add(userRefreshToken);
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = rawRefreshToken,
                Email = user.Email
            };
        }

        /// <summary>
        /// Xoay vòng và cấp phát cặp Token mới khi Access Token cũ hết thời hạn hiệu lực.
        /// </summary>
        /// <param name="request">RefreshTokenRequestDto - FE - Cặp token cũ.</param>
        /// <returns>Bộ đôi Token mới dạng AuthResponseDto nếu hợp lệ, ngược lại trả về null.</returns>
        public async Task<AuthResponseDto?> RefreshTokenAsync(RefreshTokenRequestDto request)
        {
            var principal = GetPrincipalFromExpiredToken(request.AccessToken);
            string? jwtId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (string.IsNullOrEmpty(jwtId)) return null;

            var storedToken = await _context.UserRefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.JwtId == jwtId);
            if (storedToken == null) return null;

            // KÍCH HOẠT BẪY CHỐNG GIAN LẬN: Phát hiện token đã bị gỡ niêm phong hoặc hết hạn sống
            if (storedToken.IsRevoked || storedToken.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("CẢNH BÁO BẢO MẬT: Phát hiện refresh token tái sử dụng trái phép! Tiến hành phong tỏa toàn bộ tài khoản có UserId={UserId}.", storedToken.UserId);

                var compromiseTokens = await _context.UserRefreshTokens.Where(t => t.UserId == storedToken.UserId).ToListAsync();
                foreach (var token in compromiseTokens)
                {
                    token.IsRevoked = true;
                    token.RevokedAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
                throw new BreachDetectedException("Breach detected! Force logout initiated for security defense.");
            }

            bool isRefreshTokenValid = BCrypt.Net.BCrypt.Verify(request.RefreshToken, storedToken.TokenHash);
            if (!isRefreshTokenValid) return null;

            storedToken.IsRevoked = true;
            storedToken.RevokedAt = DateTime.UtcNow;

            string newJwtId = Guid.NewGuid().ToString();
            string newAccessToken = GenerateJwtToken(storedToken.User, newJwtId);
            string newRawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

            var newUserRefreshToken = new UserRefreshToken
            {
                TokenId = Guid.NewGuid(),
                UserId = storedToken.UserId,
                TokenHash = BCrypt.Net.BCrypt.HashPassword(newRawRefreshToken),
                JwtId = newJwtId,
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = storedToken.ExpiresAt // Giữ nguyên hạn dùng gốc chống Sliding Expiry vô hạn
            };

            _context.UserRefreshTokens.Add(newUserRefreshToken);
            await _context.SaveChangesAsync();

            return new AuthResponseDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newRawRefreshToken,
                Email = storedToken.User.Email
            };
        }

        /// <summary>
        /// Hủy phiên hoạt động vĩnh viễn của Refresh Token dưới DB để phục vụ luồng đăng xuất.
        /// </summary>
        /// <param name="request">RefreshTokenRequestDto - FE - Thông tin cặp token cần hủy.</param>
        /// <returns>Trả về true nếu bẻ cờ thành công, ngược lại trả về false.</returns>
        public async Task<bool> RevokeTokenAsync(RefreshTokenRequestDto request)
        {
            var principal = GetPrincipalFromExpiredToken(request.AccessToken);
            string? jwtId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (string.IsNullOrEmpty(jwtId)) return false;

            var storedToken = await _context.UserRefreshTokens.FirstOrDefaultAsync(t => t.JwtId == jwtId);
            if (storedToken == null || storedToken.IsRevoked) return false;

            bool isRefreshTokenValid = BCrypt.Net.BCrypt.Verify(request.RefreshToken, storedToken.TokenHash);
            if (!isRefreshTokenValid) return false;

            storedToken.IsRevoked = true;
            storedToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Kiểm định tính hợp lệ của mã OTP tại bước 1 của tiến trình khôi phục mật khẩu.
        /// </summary>
        /// <param name="dto">VerifyOtpDto - FE - Email và mã OTP cần kiểm định.</param>
        /// <returns>Trả về true nếu trùng khớp OTP trong RAM, ngược lại trả về false.</returns>
        public async Task<bool> VerifyOtpAsync(VerifyOtpDto dto)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Email == dto.Email);
            if (!userExists) return false;

            string cacheKey = $"OTP_Forgot_{dto.Email}";
            if (!_cache.TryGetValue(cacheKey, out string? savedOtp)) return false;

            return savedOtp == dto.OtpCode;
        }

        /// <summary>
        /// Cập nhật chuỗi băm mật khẩu mới xuống MySQL trong tiến trình khôi phục mật khẩu.
        /// </summary>
        /// <param name="dto">ResetPasswordDto - FE - Email và thông tin mật khẩu mới.</param>
        /// <returns>Trả về true nếu cập nhật thành công, trả về false nếu trùng mật khẩu cũ.</returns>
        public async Task<bool> ResetPasswordAsync(ResetPasswordDto dto)
        {
            string cacheKey = $"OTP_Forgot_{dto.Email}";
            if (!_cache.TryGetValue(cacheKey, out string? _)) return false;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null) return false;

            bool isSamePassword = BCrypt.Net.BCrypt.Verify(dto.NewPassword, user.PasswordHash);
            if (isSamePassword) return false;

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();
            _cache.Remove(cacheKey);
            return true;
        }

        private string GenerateJwtToken(User user, string jwtId)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.RoleId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, jwtId)
            };

            var secretKey = _configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("SecretKey missing");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("SecretKey missing"))),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = false
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new SecurityTokenException("Invalid token encryption algorithm.");
            }

            return principal;
        }
        /// <summary>
        /// Luồng Quên mật khẩu Bước 0: Kiểm tra sự tồn tại của tài khoản, sinh mã OTP ngẫu nhiên và cất vào két RAM cache.
        /// </summary>
        /// <param name="request">SendOtpRequestDto - Gói dữ liệu chứa địa chỉ Email thô cần yêu cầu cấp lại mật khẩu.</param>
        /// <returns>
        /// Trả về chuỗi ký tự Email nếu tài khoản có tồn tại hợp lệ dưới MySQL; 
        /// Trả về null nếu địa chỉ Email chưa từng đăng ký hệ thống để Controller bốc lỗi 400.
        /// </returns>
        public async Task<string?> SendForgotPasswordOtpAsync(SendOtpRequestDto request)
        {
            // Chặn request rác sớm: Luồng quên mật khẩu bắt buộc tài khoản phải tồn tại trước dưới DB v7 mới cho phép đi tiếp
            var userExists = await _context.Users.AnyAsync(u => u.Email == request.Email);
            if (!userExists) return null;

            // Sử dụng bộ sinh số ngẫu nhiên CSPRNG bảo mật cao chống đoán trước chuỗi OTP mẫu
            string otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            string emailLower = request.Email.ToLower().Trim();
            string cacheKey = $"OTP_Forgot_{emailLower}";

            // TTL 5 phút cất biệt lập theo tiền tố key riêng để tuyệt đối không bị đè dữ liệu hoặc xung đột với luồng OTP đăng ký
            _cache.Set(cacheKey, otpCode, TimeSpan.FromMinutes(5));
            // Gọi Helper lấy phôi thư quên mật khẩu
            string mailSubject = "Yêu Cầu Thay Đổi Mật Khẩu - ScientificTrendTracker";
            string mailBody = EmailTemplateService.GetForgotPasswordOtpTemplate(otpCode);

            await _emailService.SendEmailAsync(request.Email, mailSubject, mailBody);

            _logger.LogInformation("MOCK OTP LUỒNG QUÊN MẬT KHẨU CHO SWAGGER TEST: {OtpCode}", otpCode);
            return request.Email;
        }
    }
}
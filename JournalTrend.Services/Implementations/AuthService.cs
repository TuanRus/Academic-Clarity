using JournalTrend.Core.DTOs;
using JournalTrend.Core.Entities;
using JournalTrend.Core.Exceptions;
using JournalTrend.Infrastructure;
using JournalTrend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace JournalTrend.Services.Implementations
{
    /// <summary>Bộ não xử lý toàn bộ các phép tính toán thô của phân hệ Xác thực tài khoản.</summary>
    public class AuthService : IAuthService
    {
        private readonly DataContext _context;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AuthService> _logger;

        public AuthService(DataContext context, IConfiguration configuration, IMemoryCache cache, ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
        }

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

            _logger.LogInformation("MOCK OTP FOR TEST SỬ DỤNG SWAGGER: {OtpCode}", otpCode);
            return request.Email;
        }

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

        public async Task<bool> VerifyOtpAsync(VerifyOtpDto dto)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Email == dto.Email);
            if (!userExists) return false;

            string cacheKey = $"OTP_Forgot_{dto.Email}";
            if (!_cache.TryGetValue(cacheKey, out string? savedOtp)) return false;

            return savedOtp == dto.OtpCode;
        }

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
                issuer: _configuration["Jwt:issuer"],
                audience: _configuration["Jwt:audience"],
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
                ValidIssuer = _configuration["Jwt:issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:audience"],
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

            _logger.LogInformation("MOCK OTP LUỒNG QUÊN MẬT KHẨU CHO SWAGGER TEST: {OtpCode}", otpCode);
            return request.Email;
        }
    }
}
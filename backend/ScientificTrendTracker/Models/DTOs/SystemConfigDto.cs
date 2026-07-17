using System.Collections.Generic;

namespace ScientificTrendTracker.Models.DTOs
{
    /// <summary>
    /// DTO cấu hình vận hành hệ thống — CHỈ chứa các trường KHÔNG bí mật để hiển thị read-only cho Admin.
    /// Tuyệt đối không đưa secret (connection string, API key, JWT secret, mật khẩu email...) vào đây.
    /// </summary>
    public class SystemConfigDto
    {
        // ----- Môi trường chạy -----
        public string Environment { get; set; }
        public string DotnetVersion { get; set; }
        public string AllowedHosts { get; set; }
        public string DefaultLogLevel { get; set; }

        // ----- OpenAlex (không bí mật) -----
        public string OpenAlexBaseUrl { get; set; }
        public string OpenAlexEmail { get; set; }

        // ----- Đồng bộ tự động -----
        public bool WeeklySyncEnabled { get; set; }

        // ----- JWT (chỉ Issuer/Audience, KHÔNG SecretKey) -----
        public string JwtIssuer { get; set; }
        public string JwtAudience { get; set; }

        // ----- Nhà cung cấp AI (KHÔNG ApiKey) -----
        public List<AiProviderInfoDto> AiProviders { get; set; } = new();
        public string GeminiModel { get; set; }
        public string GeminiBaseUrl { get; set; }
        public int GeminiTimeoutSeconds { get; set; }

        // ----- Trạng thái tích hợp: chỉ báo "đã cấu hình" (bool), KHÔNG lộ giá trị -----
        public List<IntegrationStatusDto> Integrations { get; set; } = new();
    }

    public class AiProviderInfoDto
    {
        public string Name { get; set; }
        public string BaseUrl { get; set; }
        public string Model { get; set; }
    }

    public class IntegrationStatusDto
    {
        public string Name { get; set; }
        public bool Configured { get; set; }
    }
}

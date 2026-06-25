using System;

namespace ScientificTrendTracker.BackgroundServices
{
    /// <summary>
    /// Ngoại lệ nghiệp vụ đặc thù kích hoạt khi phát hiện hành vi tái sử dụng Refresh Token cũ 
    /// - Dấu hiệu cảnh báo hệ thống đang bị hacker tấn công đánh cắp chuỗi bảo mật.
    /// </summary>
    public class BreachDetectedException : Exception
    {
        public BreachDetectedException(string message) : base(message) { }
    }
}
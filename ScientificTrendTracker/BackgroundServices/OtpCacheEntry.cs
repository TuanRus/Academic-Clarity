using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScientificTrendTracker.BackgroundServices
{
    /// <summary>
    /// Cấu trúc lưu trữ thông tin mã OTP tạm thời trên RAM bộ nhớ đệm.
    /// </summary>
    /// <param name="OtpCode">Mã OTP gồm 6 chữ số sinh ngẫu nhiên.</param>
    /// <param name="CreatedAt">Thời điểm mã OTP được tạo (Giờ UTC).</param>
    /// <param name="IsEduEmail">Đánh dấu địa chỉ email có đuôi mở rộng dạng .edu học thuật hay không.</param>
    public record OtpCacheEntry
    (
        string OtpCode,
        DateTime CreatedAt,
        bool IsEduEmail
    );
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JournalTrend.Core.DTOs
{
    /// <summary>
    /// Cấu trúc lưu trữ thông tin mã OTP tạm thời trên RAM bộ nhớ đệm
    /// </summary>
    public record OtpCacheEntry
    (
        string OtpCode,
        DateTime CreatedAt,
        bool IsEduEmail
    );
}

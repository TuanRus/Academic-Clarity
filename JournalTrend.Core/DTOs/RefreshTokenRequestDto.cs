using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JournalTrend.Core.DTOs
{
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

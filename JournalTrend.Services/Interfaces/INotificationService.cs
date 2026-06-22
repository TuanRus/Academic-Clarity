using System.Collections.Generic;
using System.Threading.Tasks;
using JournalTrend.Core.DTOs;

namespace JournalTrend.Services.Interfaces
{
    /// <summary>
    /// Giao diện định nghĩa các nghiệp vụ liên quan đến 
    /// hệ thống thông báo và hộp chuông cấu hình real-time.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Quét danh sách học giả đang theo dõi chuyên mục/tạp chí 
        /// liên quan đến bài báo mới, tiến hành lưu vết lịch sử 
        /// thông báo xuống cơ sở dữ liệu MySQL.
        /// </summary>
        /// <param name="trigger">Gói DTO chứa thông tin bài báo mới phát hành.</param>
        /// <returns>Danh sách thô chứa các mã UserId cần nhận thông báo real-time.</returns>
        Task<List<int>> CheckAndPushAsync(NotificationTriggerDto trigger);
    }
}
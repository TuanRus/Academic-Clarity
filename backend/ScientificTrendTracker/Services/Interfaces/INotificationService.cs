using System.Collections.Generic;
using System.Threading.Tasks;
using ScientificTrendTracker.Models.DTOs;

namespace ScientificTrendTracker.Services.Interfaces
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
        /// <param name="trigger">NotificationTriggerDto - Hệ thống quét / Admin - Gói DTO chứa thông tin bài báo mới phát hành.</param>
        /// <returns>Danh sách thô chứa các mã UserId cần nhận thông báo real-time.</returns>
        Task<List<int>> CheckAndPushAsync(NotificationTriggerDto trigger);

        /// <summary>Lấy thông báo của 1 user (mới nhất trước).</summary>
        Task<List<NotificationItemDto>> GetMyNotificationsAsync(int userId, int limit = 30);

        /// <summary>Đếm số thông báo chưa đọc của user (cho badge chuông).</summary>
        Task<int> GetUnreadCountAsync(int userId);

        /// <summary>Đánh dấu 1 thông báo đã đọc. Trả false nếu không thuộc user / không tồn tại.</summary>
        Task<bool> MarkReadAsync(int userId, int notificationId);

        /// <summary>Đánh dấu tất cả thông báo của user là đã đọc. Trả số bản ghi cập nhật.</summary>
        Task<int> MarkAllReadAsync(int userId);
    }
}
using System.Collections.Generic;
using System.Threading.Tasks;
using JournalTrend.Core.DTOs;

namespace JournalTrend.Services.Interfaces
{
    /// <summary>Hợp đồng quản lý luồng tính toán quét tệp người dùng tương tác.</summary>
    public interface INotificationService
    {
        /// <summary>Quét follower hệ thống và thực hiện Bulk Insert. Trả về danh sách mảng UserId nhận thông báo, hoặc list rỗng.</summary>
        Task<List<int>> CheckAndPushAsync(NotificationTriggerDto trigger);
    }
}
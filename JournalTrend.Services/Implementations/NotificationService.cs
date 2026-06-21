using JournalTrend.Core.DTOs;
using JournalTrend.Infrastructure;
using JournalTrend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JournalTrend.Services.Implementations
{
    /// <summary>Dịch vụ trần tính toán trích xuất danh sách tệp người dùng thỏa mãn điều kiện theo dõi.</summary>
    public class NotificationService : INotificationService
    {
        private readonly DataContext _context;

        public NotificationService(DataContext context)
        {
            _context = context;
        }

        public async Task<List<int>> CheckAndPushAsync(NotificationTriggerDto trigger)
        {
            // Quét móng bảng followed_items theo cấu trúc DB v7 đã được thiết lập Index kép tối ưu
            var userIdsToNotify = await _context.FollowedItems
                .Where(f => (f.TargetType == "journal" && f.JournalId == trigger.JournalId) ||
                            (f.TargetType == "topic" && trigger.TopicIds.Contains(f.TopicId!)))
                .Select(f => f.UserId)
                .Distinct()
                .ToListAsync();

            if (!userIdsToNotify.Any()) return userIdsToNotify; // Trả về list rỗng nếu không có ai theo dõi

            string notifTitle = "New Publication Detected!";
            string notifMessage = $"The paper '{trigger.PaperTitle}' has been published matching your followed journals or topics.";

            var newNotifications = new List<JournalTrend.Core.Entities.Notification>();
            foreach (var userId in userIdsToNotify)
            {
                newNotifications.Add(new JournalTrend.Core.Entities.Notification
                {
                    UserId = userId,
                    Title = notifTitle,
                    Message = notifMessage,
                    RelatedPaperId = trigger.PaperId,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Găm chặt lịch sử chuông thông báo vào ổ đĩa Linux thông qua cơ chế Bulk Insert AddRange
            _context.Notifications.AddRange(newNotifications);
            await _context.SaveChangesAsync();

            return userIdsToNotify;
        }
    }
}
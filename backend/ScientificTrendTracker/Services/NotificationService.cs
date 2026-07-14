using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Services.Interfaces;
using ScientificTrendTracker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Lớp thực thi các nghiệp vụ quản lý và phân phối thông báo hệ thống.
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<NotificationService> _logger;

        /// <summary>
        /// Khởi tạo dịch vụ quản lý thông báo.
        /// </summary>
        /// <param name="context">AppDbContext - Database - Context kết nối DB chính.</param>
        /// <param name="logger">ILogger - System - Dịch vụ ghi log.</param>
        public NotificationService(
            AppDbContext context,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Thực hiện quét đối tượng theo dõi đa hình và bulk insert thông báo.
        /// </summary>
        /// <param name="trigger">NotificationTriggerDto - Hệ thống quét / Admin - Gói DTO chứa thông tin bài báo mới phát hành.</param>
        /// <returns>Danh sách thô chứa các mã UserId cần nhận thông báo real-time.</returns>
        public async Task<List<int>> CheckAndPushAsync(
            NotificationTriggerDto trigger)
        {
            // Giữ nguyên hoa/thường: FollowService lưu Topic/JournalId đúng như FE gửi (không lowercase).
            string? journalIdClean = trigger.JournalId?.Trim();
            string paperIdClean = trigger.PaperId?.Trim() ?? string.Empty;
            bool hasJournal = !string.IsNullOrEmpty(journalIdClean);

            List<string> topicIdsClean = trigger.TopicIds?
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList() ?? new List<string>();

            List<int> authorIds = trigger.AuthorIds ?? new List<int>();

            // Quét follower của TÁC GIẢ / TẠP CHÍ / CHỦ ĐỀ khớp với bài báo mới.
            var followedItems = await _context.FollowedItems
                .AsNoTracking()
                .Where(f =>
                    (hasJournal && f.TargetType == "journal" && f.JournalId == journalIdClean) ||
                    (topicIdsClean.Any() && f.TargetType == "topic" && topicIdsClean.Contains(f.TopicId ?? string.Empty)) ||
                    (authorIds.Any() && f.TargetType == "author" && f.AuthorId != null && authorIds.Contains(f.AuthorId.Value)))
                .Select(f => new { f.UserId, f.FollowId, f.TargetType })
                .ToListAsync();

            if (followedItems.Count == 0)
            {
                _logger.LogInformation("Không có học giả nào đăng ký nhận tin cho bài báo này.");
                return new List<int>();
            }

            // 1 thông báo / user (dedup) — chọn loại đối tượng đầu tiên khớp để soạn message tiếng Anh.
            var perUser = followedItems.GroupBy(x => x.UserId).Select(g => g.First()).ToList();

            static string MessageFor(string targetType, string paperTitle) => targetType switch
            {
                "author" => $"New paper from an author you follow: \"{paperTitle}\".",
                "journal" => $"New paper in a journal you follow: \"{paperTitle}\".",
                _ => $"New paper on a topic you follow: \"{paperTitle}\".",
            };

            var notificationsToCreate = perUser.Select(item => new Notification
            {
                UserId = item.UserId,
                FollowedItemId = item.FollowId,
                Title = "New research update",
                Message = MessageFor(item.TargetType, trigger.PaperTitle),
                RelatedPaperId = paperIdClean, // link tới bài báo
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            await _context.Notifications.AddRangeAsync(notificationsToCreate);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Đã tạo {Count} thông báo bài báo mới cho follower.", notificationsToCreate.Count);
            return perUser.Select(item => item.UserId).Distinct().ToList();
        }

        /// <summary>Gửi 1 thông báo giống nhau tới MỌI user đang hoạt động (system broadcast của Admin).</summary>
        public async Task<List<int>> BroadcastAsync(string title, string message)
        {
            var userIds = await _context.Users
                .Where(u => u.IsActive)
                .Select(u => u.UserId)
                .ToListAsync();

            if (userIds.Count == 0) return new List<int>();

            var now = DateTime.UtcNow;
            var notifs = userIds.Select(uid => new Notification
            {
                UserId = uid,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = now
            }).ToList();

            await _context.Notifications.AddRangeAsync(notifs);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin broadcast: đã gửi tới {Count} user.", userIds.Count);
            return userIds;
        }

        public async Task<List<NotificationItemDto>> GetMyNotificationsAsync(int userId, int limit = 30)
        {
            return await _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .Select(n => new NotificationItemDto
                {
                    NotificationId = n.NotificationId,
                    Title = n.Title,
                    Message = n.Message,
                    RelatedPaperId = n.RelatedPaperId,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(int userId)
            => await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        public async Task<bool> MarkReadAsync(int userId, int notificationId)
        {
            var n = await _context.Notifications
                .FirstOrDefaultAsync(x => x.NotificationId == notificationId && x.UserId == userId);
            if (n == null) return false;
            if (!n.IsRead)
            {
                n.IsRead = true;
                n.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return true;
        }

        public async Task<int> MarkAllReadAsync(int userId)
        {
            var unread = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();
            foreach (var n in unread) { n.IsRead = true; n.ReadAt = DateTime.UtcNow; }
            if (unread.Count > 0) await _context.SaveChangesAsync();
            return unread.Count;
        }
    }
}
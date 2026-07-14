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
            // Ép lowercase + trim vì dữ liệu từ hệ thống thu thập tự động 
            // và form admin không đồng nhất về định dạng khoảng trắng.
            string? journalIdClean = trigger.JournalId?.Trim().ToLower();
            string paperIdClean = trigger.PaperId?.Trim() ?? string.Empty;
            bool hasJournal = !string.IsNullOrEmpty(journalIdClean);

            List<string> topicIdsClean = trigger.TopicIds?
                .Select(id => id.Trim().ToLower())
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList() ?? new List<string>();

            // Chỉ kích hoạt điều kiện lọc khi thực sự tồn tại JournalId hoặc TopicId 
            // nhằm triệt tiêu rủi ro quét nhầm dữ liệu trống rỗng dưới DB.
            var followedItems = await _context.FollowedItems
                .AsNoTracking()
                .Where(f =>
                    (hasJournal && f.TargetType == "journal"
                        && f.JournalId == journalIdClean) ||
                    (topicIdsClean.Any() && f.TargetType == "topic"
                        && topicIdsClean.Contains(f.TopicId ?? string.Empty)))
                .Select(f => new { f.UserId, f.FollowId })
                .ToListAsync();

            if (followedItems.Count == 0)
            {
                _logger.LogInformation(
                    "Không có học giả nào đăng ký nhận tin cho bài báo này.");
                return new List<int>(); // [Mục 5] Trả về mảng rỗng thô thay vì ném ngoại lệ
            }

            var notificationsToCreate = new List<Notification>();
            string titleMsg = "Bài báo nghiên cứu mới phát hành";
            string contentMsg = $"Bài báo '{trigger.PaperTitle}' vừa được xuất bản.";

            foreach (var item in followedItems)
            {
                notificationsToCreate.Add(new Notification
                {
                    UserId = item.UserId,
                    FollowedItemId = item.FollowId,
                    Title = titleMsg,
                    Message = contentMsg,
                    RelatedPaperId = paperIdClean,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow // [Mục 7] Đồng bộ giờ UTC tránh lệch múi giờ
                });
            }

            // Dùng AddRange để EF Core gom cụm lệnh insert vào 1 transaction duy nhất,
            // triệt tiêu chi phí round-trip đường truyền mạng ảo qua Tailscale.
            await _context.Notifications.AddRangeAsync(notificationsToCreate);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Đã bulk insert thành công {Count} thông báo vào hệ thống.",
                notificationsToCreate.Count);

            // Gom cụm Distinct ở RAM sau khi insert xong để bảo đảm trích xuất 
            // danh sách UserId duy nhất đẩy lên Hub kích nổ real-time.
            var uniqueUserIds = followedItems
                .Select(item => item.UserId)
                .Distinct()
                .ToList();

            return uniqueUserIds; // [Mục 1] Trả về raw type đúng phân tách trách nhiệm
        }
    }
}
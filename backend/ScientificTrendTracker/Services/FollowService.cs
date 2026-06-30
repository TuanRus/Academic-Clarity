using System;
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
    /// Lớp thực thi các nghiệp vụ liên quan đến hành vi Theo dõi.
    /// </summary>
    public class FollowService : IFollowService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FollowService> _logger;

        /// <summary>
        /// Khởi tạo dịch vụ theo dõi đối tượng khoa học.
        /// </summary>
        /// <param name="context">AppDbContext - Database - Context kết nối DB chính.</param>
        /// <param name="logger">ILogger - System - Dịch vụ ghi log.</param>
        public FollowService(
            AppDbContext context,
            ILogger<FollowService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Thực hiện đảo trạng thái Theo dõi/Hủy theo dõi (Toggle).
        /// </summary>
        /// <param name="userId">int - DB/Token - ID duy nhất của người dùng thực hiện Toggle.</param>
        /// <param name="request">ToggleFollowDto - FE - Gói dữ liệu chứa thông tin loại đối tượng và ID đối tượng cần Toggle.</param>
        /// <returns>
        /// Trả về DTO kết quả thô kèm số lượng follower; 
        /// Trả về null nếu loại mục tiêu không hợp lệ hoặc không tìm thấy ID.
        /// </returns>
        public async Task<FollowResultDto?> ToggleFollowAsync(
            int userId,
            ToggleFollowDto request)
        {
            // Tránh lỗi tìm kiếm và so khớp do dữ liệu từ Client có thể chứa khoảng trắng hoặc chữ hoa/thường không chuẩn
            string typeClean = request.TargetType.Trim().ToLower();
            string idClean = request.TargetId.Trim();

            // Fail-fast: kiểm tra sự tồn tại của đối tượng trước khi thực hiện toggle để tránh tạo mối quan hệ rác
            if (typeClean == "topic")
            {
                var topicExists = await _context.ResearchTopics
                    .AnyAsync(t => t.TopicId == idClean);
                if (!topicExists) return null;
            }
            else if (typeClean == "journal")
            {
                var journalExists = await _context.Journals
                    .AnyAsync(j => j.JournalId == idClean);
                if (!journalExists) return null;
            }
            else
            {
                return null;
            }

            var existingFollow = await _context.FollowedItems
                .FirstOrDefaultAsync(f =>
                    f.UserId == userId &&
                    f.TargetType == typeClean &&
                    (typeClean == "topic"
                        ? f.TopicId == idClean
                        : f.JournalId == idClean));

            bool isFollowingResult;

            if (existingFollow != null)
            {
                _context.FollowedItems.Remove(existingFollow);
                isFollowingResult = false;

                _logger.LogInformation(
                    "User {Uid} hủy theo dõi {Type} {Id}",
                    userId, typeClean, idClean);
            }
            else
            {
                var newFollow = new FollowedItem
                {
                    UserId = userId,
                    TargetType = typeClean,
                    TopicId = typeClean == "topic" ? idClean : null,
                    JournalId = typeClean == "journal" ? idClean : null,
                    CreatedAt = DateTime.UtcNow // UTC tránh lệch múi giờ giữa máy chủ và client
                };

                _context.FollowedItems.Add(newFollow);
                isFollowingResult = true;

                _logger.LogInformation(
                    "User {Uid} theo dõi mới {Type} {Id}",
                    userId, typeClean, idClean);
            }

            await _context.SaveChangesAsync();

            // Thực hiện hoàn toàn dưới Database để giảm băng thông truyền dữ liệu và tối ưu RAM máy chủ
            int totalFollowers = await _context.FollowedItems
                .CountAsync(f =>
                    f.TargetType == typeClean &&
                    (typeClean == "topic"
                        ? f.TopicId == idClean
                        : f.JournalId == idClean));

            return new FollowResultDto
            {
                IsFollowing = isFollowingResult,
                TotalFollowers = totalFollowers
            };
        }

        public async Task<System.Collections.Generic.List<FollowedItemDto>> GetMyFollowsAsync(int userId)
        {
            return await _context.FollowedItems
                .AsNoTracking()
                .Where(f => f.UserId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new FollowedItemDto
                {
                    FollowId = f.FollowId,
                    TargetType = f.TargetType,
                    TargetId = f.TargetType == "topic" ? f.TopicId! : f.JournalId!,
                    Name = f.TargetType == "topic"
                        ? (f.ResearchTopic != null ? f.ResearchTopic.TopicName : f.TopicId!)
                        : (f.Journal != null ? f.Journal.JournalName : f.JournalId!)
                })
                .ToListAsync();
        }
    }
}
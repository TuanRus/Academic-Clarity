using System;
using System.Threading.Tasks;
using JournalTrend.Core.DTOs;
using JournalTrend.Core.Entities;
using JournalTrend.Services.Interfaces;
using JournalTrend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JournalTrend.Services.Implementations
{
    /// <summary>
    /// Lớp thực thi các nghiệp vụ liên quan đến hành vi Theo dõi[cite: 54].
                /// </summary>
    public class FollowService : IFollowService
    {
        private readonly DataContext _context;
        private readonly ILogger<FollowService> _logger;

        public FollowService(
            DataContext context,
            ILogger<FollowService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Thực hiện đảo trạng thái Theo dõi/Hủy theo dõi (Toggle)[cite: 54].
                    /// </summary>
        public async Task<FollowResultDto?> ToggleFollowAsync(
            int userId,
            ToggleFollowDto request)
        {
            // 1. Chuẩn hóa dữ liệu đầu vào từ biên Request (Mục 4 Quy tắc) [cite: 84]
            string typeClean = request.TargetType.Trim().ToLower();
            string idClean = request.TargetId.Trim();

            // 2. Chốt chặn kiểm tra tồn tại (Fail-Fast) -> Sai trả null (Mục 5 Quy tắc) [cite: 84, 107]
            if (typeClean == "topic")
            {
                var topicExists = await _context.ResearchTopics
                    .AnyAsync(t => t.TopicId == idClean); // Lọc Async LINQ [cite: 106]
                if (!topicExists) return null; // Không thấy -> null [cite: 109, 116]
            }
            else if (typeClean == "journal")
            {
                var journalExists = await _context.Journals
                    .AnyAsync(j => j.JournalId == idClean);
                if (!journalExists) return null; // Không thấy -> null [cite: 109, 116]
            }
            else
            {
                return null; // Loại mục tiêu không hợp lệ
            }

            // 3. Tìm kiếm bản ghi theo dõi hiện tại dưới DB
            var existingFollow = await _context.FollowedItems
                .FirstOrDefaultAsync(f =>
                    f.UserId == userId &&
                    f.TargetType == typeClean &&
                    (typeClean == "topic"
                        ? f.TopicId == idClean
                        : f.JournalId == idClean));

            bool isFollowingResult;

            // 4. Tiến hành đảo trạng thái (Toggle Logic)
            if (existingFollow != null)
            {
                _context.FollowedItems.Remove(existingFollow);
                isFollowingResult = false;

                // Structured logging ghi nhận hành động (Mục 8 Quy tắc) [cite: 176, 187]
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
                    CreatedAt = DateTime.UtcNow // Luôn lưu giờ UTC (Mục 7 Quy tắc) [cite: 151, 160]
                };

                _context.FollowedItems.Add(newFollow);
                isFollowingResult = true;

                // Structured logging ghi nhận hành động [cite: 176, 187]
                _logger.LogInformation(
                    "User {Uid} theo dõi mới {Type} {Id}",
                    userId, typeClean, idClean);
            }

            // Lưu thay đổi xuống MySQL xuyên suốt VPN Tailscale [cite: 216]
            await _context.SaveChangesAsync();

            // 5. Tính toán trên DB, không tính trên RAM (Mục 5 Quy tắc) [cite: 123]
            int totalFollowers = await _context.FollowedItems
                .CountAsync(f =>
                    f.TargetType == typeClean &&
                    (typeClean == "topic"
                        ? f.TopicId == idClean
                        : f.JournalId == idClean));

            // Trả về DTO trần đúng quy định phân tách trách nhiệm [cite: 9, 107]
            return new FollowResultDto
            {
                IsFollowing = isFollowingResult,
                TotalFollowers = totalFollowers
            };
        }
    }
}
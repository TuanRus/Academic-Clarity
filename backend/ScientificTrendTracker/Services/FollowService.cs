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
            int? authorId = null;

            // Fail-fast: kiểm tra sự tồn tại của đối tượng trước khi thực hiện toggle để tránh tạo mối quan hệ rác
            if (typeClean == "topic")
            {
                // idClean có thể là TopicId sẵn có HOẶC TopicName (FE gửi từ trang Paper Detail, nơi
                // bài báo chỉ phơi ra tên topic). Resolve theo Id trước, rồi theo Tên; nếu chưa có thì
                // tự tạo ResearchTopic mới (giống cách journal/keyword được auto-create khi thêm bài).
                var topic = await _context.ResearchTopics
                    .FirstOrDefaultAsync(t => t.TopicId == idClean || t.TopicName == idClean);
                if (topic == null)
                {
                    topic = new ResearchTopic
                    {
                        TopicId = Guid.NewGuid().ToString("N")[..20],
                        TopicName = idClean,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.ResearchTopics.Add(topic);
                    await _context.SaveChangesAsync();
                }
                idClean = topic.TopicId; // chuẩn hoá về TopicId để toggle/dedup nhất quán theo khoá chính
            }
            else if (typeClean == "journal")
            {
                if (!await _context.Journals.AnyAsync(j => j.JournalId == idClean)) return null;
            }
            else if (typeClean == "author")
            {
                if (!int.TryParse(idClean, out var aid)) return null;
                if (!await _context.Authors.AnyAsync(a => a.AuthorId == aid)) return null;
                authorId = aid;
            }
            else
            {
                return null;
            }

            // Tách rõ từng nhánh (không ternary trong LINQ để EF dịch SQL ổn định).
            FollowedItem? existingFollow = typeClean switch
            {
                "topic" => await _context.FollowedItems.FirstOrDefaultAsync(f =>
                    f.UserId == userId && f.TargetType == typeClean && f.TopicId == idClean),
                "journal" => await _context.FollowedItems.FirstOrDefaultAsync(f =>
                    f.UserId == userId && f.TargetType == typeClean && f.JournalId == idClean),
                _ => await _context.FollowedItems.FirstOrDefaultAsync(f =>
                    f.UserId == userId && f.TargetType == typeClean && f.AuthorId == authorId),
            };

            bool isFollowingResult;

            if (existingFollow != null)
            {
                _context.FollowedItems.Remove(existingFollow);
                isFollowingResult = false;
                _logger.LogInformation("User {Uid} hủy theo dõi {Type} {Id}", userId, typeClean, idClean);
            }
            else
            {
                _context.FollowedItems.Add(new FollowedItem
                {
                    UserId = userId,
                    TargetType = typeClean,
                    TopicId = typeClean == "topic" ? idClean : null,
                    JournalId = typeClean == "journal" ? idClean : null,
                    AuthorId = authorId,
                    CreatedAt = DateTime.UtcNow
                });
                isFollowingResult = true;
                _logger.LogInformation("User {Uid} theo dõi mới {Type} {Id}", userId, typeClean, idClean);
            }

            await _context.SaveChangesAsync();

            int totalFollowers = typeClean switch
            {
                "topic" => await _context.FollowedItems.CountAsync(f => f.TargetType == typeClean && f.TopicId == idClean),
                "journal" => await _context.FollowedItems.CountAsync(f => f.TargetType == typeClean && f.JournalId == idClean),
                _ => await _context.FollowedItems.CountAsync(f => f.TargetType == typeClean && f.AuthorId == authorId),
            };

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
                    TargetId = f.TargetType == "topic" ? f.TopicId!
                        : f.TargetType == "journal" ? f.JournalId!
                        : (f.AuthorId != null ? f.AuthorId.ToString()! : ""),
                    Name = f.TargetType == "topic"
                        ? (f.ResearchTopic != null ? f.ResearchTopic.TopicName : f.TopicId!)
                        : f.TargetType == "journal"
                            ? (f.Journal != null ? f.Journal.JournalName : f.JournalId!)
                            : (f.Author != null ? f.Author.FullName : (f.AuthorId != null ? f.AuthorId.ToString()! : "")),
                })
                .ToListAsync();
        }
    }
}
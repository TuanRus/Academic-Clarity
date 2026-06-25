using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs.Keyword;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Triển khai chi tiết các nghiệp vụ CRUD quản lý từ khóa dành cho Admin.
    /// </summary>
    public class KeywordService : IKeywordService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<KeywordService> _logger;

        public KeywordService(AppDbContext dbContext, ILogger<KeywordService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<PagedResult<KeywordAdminDto>> GetKeywordsForAdminAsync(string search, int page, int pageSize, CancellationToken ct)
        {
            var query = _dbContext.Keywords.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(k => k.KeywordName.Contains(search.Trim()));
            }

            int totalCount = await query.CountAsync(ct);

            var items = await query
                .OrderByDescending(k => k.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(k => new KeywordAdminDto
                {
                    KeywordId = k.KeywordId,
                    KeywordName = k.KeywordName,
                    AssociatedPapersCount = k.PaperKeywords.Count,
                    CreatedAt = k.CreatedAt
                })
                .ToListAsync(ct);

            return new PagedResult<KeywordAdminDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<bool> CreateKeywordAsync(CreateKeywordDto dto, CancellationToken ct)
        {
            bool isDuplicate = await _dbContext.Keywords
                .AnyAsync(k => k.KeywordName.ToLower() == dto.KeywordName.Trim().ToLower(), ct);

            if (isDuplicate)
            {
                _logger.LogWarning("Tạo từ khóa thất bại: Tên từ khóa '{Name}' đã tồn tại.", dto.KeywordName);
                return false;
            }

            var keyword = new Keyword
            {
                KeywordId = Guid.NewGuid().ToString("N")[..20],
                KeywordName = dto.KeywordName.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Keywords.Add(keyword);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Admin tạo thành công từ khóa mới: '{Name}' (ID: {Id})", keyword.KeywordName, keyword.KeywordId);
            return true;
        }

        public async Task<bool> UpdateKeywordAsync(string keywordId, UpdateKeywordDto dto, CancellationToken ct)
        {
            var keyword = await _dbContext.Keywords.FindAsync(new object[] { keywordId }, ct);
            if (keyword == null)
            {
                _logger.LogWarning("Cập nhật thất bại: Không tìm thấy từ khóa ID {KeywordId}.", keywordId);
                return false;
            }

            // Kiểm tra trùng tên với từ khóa khác
            bool isDuplicate = await _dbContext.Keywords
                .AnyAsync(k => k.KeywordName.ToLower() == dto.KeywordName.Trim().ToLower() && k.KeywordId != keywordId, ct);

            if (isDuplicate)
            {
                _logger.LogWarning("Cập nhật thất bại: Tên từ khóa mới '{Name}' đã bị trùng.", dto.KeywordName);
                return false;
            }

            keyword.KeywordName = dto.KeywordName.Trim();
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Admin cập nhật thành công từ khóa ID {KeywordId} thành: '{Name}'", keywordId, keyword.KeywordName);
            return true;
        }

        public async Task<bool> DeleteKeywordAsync(string keywordId, CancellationToken ct)
        {
            var keyword = await _dbContext.Keywords.FindAsync(new object[] { keywordId }, ct);
            if (keyword == null)
            {
                _logger.LogWarning("Xóa thất bại: Không tìm thấy từ khóa ID {KeywordId}.", keywordId);
                return false;
            }

            // Xóa các liên kết trung gian trong PaperKeywords tránh lỗi khóa ngoại
            var paperKeywords = await _dbContext.PaperKeywords.Where(pk => pk.KeywordId == keywordId).ToListAsync(ct);
            _dbContext.PaperKeywords.RemoveRange(paperKeywords);

            // Xóa Bookmarks liên quan
            var bookmarks = await _dbContext.Bookmarks
                .Where(b => b.TargetType == "keyword" && b.KeywordId == keywordId)
                .ToListAsync(ct);
            _dbContext.Bookmarks.RemoveRange(bookmarks);

            _dbContext.Keywords.Remove(keyword);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Admin xóa thành công từ khóa ID {KeywordId}.", keywordId);
            return true;
        }
    }
}

namespace ScientificTrendTracker.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScientificTrendTracker.Models.DTOs.Search;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Lưu lịch sử tìm kiếm và trả gợi ý dựa trên history của user.
/// </summary>
public class SearchHistoryService : ISearchHistoryService
{
    private readonly AppDbContext _db;
    private readonly ILogger<SearchHistoryService> _logger;

    /// <summary>
    /// Khởi tạo SearchHistoryService và inject DbContext cùng Logger.
    /// </summary>
    /// <param name="db">
    /// AppDbContext - NGUỒN: ASP.NET Core DI Container. Kết nối cơ sở dữ liệu chính.
    /// </param>
    /// <param name="logger">
    /// ILogger - NGUỒN: ASP.NET Core DI Container. Ghi nhận lịch sử tìm kiếm.
    /// </param>
    public SearchHistoryService(
        AppDbContext db,
        ILogger<SearchHistoryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Lưu 1 lần tìm kiếm vào history</summary>
    /// <param name="userId">Số nguyên Int - NGUỒN: Controller truyền vào (lấy từ xác thực JWT).</param>
    /// <param name="dto">SearchHistoryRequestDto - NGUỒN: FE truyền lên qua request body.</param>
    /// <param name="ct">CancellationToken - NGUỒN: Hệ thống tự động quản lý vòng đời tác vụ.</param>
    public async Task SaveAsync(
        int userId,
        SearchHistoryRequestDto dto,
        CancellationToken ct = default)
    {
        var validTypes = new[] { "keyword", "author", "journal", "doi", "openalex_id" };
        if (!validTypes.Contains(dto.SearchType))
            dto.SearchType = "keyword";

        _db.SearchHistories.Add(new SearchHistory
        {
            UserId     = userId,
            SearchText = dto.SearchText.Trim(),
            SearchType = dto.SearchType,
            SearchedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[SearchHistory] User {Id} tìm: '{Text}'", userId, dto.SearchText);
    }

    /// <summary>Lấy lịch sử tìm kiếm của user (mới nhất trước)</summary>
    /// <param name="userId">Số nguyên Int - NGUỒN: Controller truyền vào.</param>
    /// <param name="limit">Số nguyên Int - Số lượng lịch sử tối đa cần lấy.</param>
    /// <param name="ct">CancellationToken - NGUỒN: Hệ thống tự động quản lý.</param>
    /// <returns>Danh sách SearchHistoryResponseDto chứa lịch sử tìm kiếm của người dùng.</returns>
    public async Task<List<SearchHistoryResponseDto>> GetHistoryAsync(
        int userId,
        int limit = 20,
        CancellationToken ct = default)
    {
        return await _db.SearchHistories
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.SearchedAt)
            .Take(limit)
            .Select(s => new SearchHistoryResponseDto
            {
                SearchHistoryId = s.SearchHistoryId,
                SearchText = s.SearchText,
                SearchType = s.SearchType,
                SearchedAt = s.SearchedAt
            })
            .ToListAsync(ct);
    }

    /// <summary>Xóa 1 lịch sử tìm kiếm</summary>
    /// <param name="userId">Số nguyên Int - NGUỒN: Controller truyền vào.</param>
    /// <param name="historyId">Số nguyên Int - ID lịch sử cần xóa.</param>
    /// <param name="ct">CancellationToken - NGUỒN: Hệ thống tự động quản lý.</param>
    /// <returns>Boolean — true nếu xóa thành công, false nếu không tìm thấy bản ghi.</returns>
    public async Task<bool> DeleteOneAsync(
        int userId, int historyId, CancellationToken ct = default)
    {
        var item = await _db.SearchHistories
            .FirstOrDefaultAsync(s => s.SearchHistoryId == historyId && s.UserId == userId, ct);

        if (item == null) return false;
        
        _db.SearchHistories.Remove(item);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Xóa toàn bộ lịch sử của user</summary>
    /// <param name="userId">Số nguyên Int - NGUỒN: Controller truyền vào.</param>
    /// <param name="ct">CancellationToken - NGUỒN: Hệ thống tự động quản lý.</param>
    public async Task DeleteAllAsync(int userId, CancellationToken ct = default)
    {
        var items = await _db.SearchHistories
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);

        if (items.Any())
        {
            _db.SearchHistories.RemoveRange(items);
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Gợi ý từ khóa dựa trên lịch sử tìm kiếm của user.
    /// Trả về top 5 từ khóa hay tìm nhất khớp với prefix.
    /// </summary>
    /// <param name="userId">Số nguyên Int - NGUỒN: Controller truyền vào.</param>
    /// <param name="prefix">Chuỗi String - Phần đầu của từ khóa cần tìm gợi ý.</param>
    /// <param name="ct">CancellationToken - NGUỒN: Hệ thống tự động quản lý.</param>
    /// <returns>Danh sách gợi ý SearchSuggestionDto.</returns>
    public async Task<List<SearchSuggestionDto>> GetSuggestionsAsync(
        int userId,
        string prefix,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return new List<SearchSuggestionDto>();

        return await _db.SearchHistories
            .Where(s => s.UserId == userId
                     && s.SearchType == "keyword"
                     && s.SearchText.StartsWith(prefix))
            .GroupBy(s => s.SearchText)
            .Select(g => new SearchSuggestionDto
            {
                SearchText = g.Key,
                Count      = g.Count()
            })
            .OrderByDescending(s => s.Count)
            .Take(5)
            .ToListAsync(ct);
    }
}




namespace ScientificTrendTracker.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScientificTrendTracker.Models.DTOs.Bookmark;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Xử lý logic bookmark bài báo hoặc keyword cho user.
/// Mỗi user chỉ bookmark 1 lần / 1 paper hoặc keyword (unique).
/// </summary>
public class BookmarkService : IBookmarkService
{
    private readonly AppDbContext _db;
    private readonly ILogger<BookmarkService> _logger;

    /// <summary>
    /// Khởi tạo BookmarkService và inject DbContext cùng Logger.
    /// </summary>
    /// <param name="db">
    /// AppDbContext - NGUỒN: ASP.NET Core DI Container. Kết nối cơ sở dữ liệu chính.
    /// </param>
    /// <param name="logger">
    /// ILogger - NGUỒN: ASP.NET Core DI Container. Ghi nhận thông tin bookmark.
    /// </param>
    public BookmarkService(
        AppDbContext db,
        ILogger<BookmarkService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Lấy toàn bộ bookmark (bài báo hoặc từ khóa) của một người dùng cụ thể.
    /// </summary>
    /// <param name="userId">
    /// Số nguyên Int - NGUỒN: Controller truyền vào (được lấy từ thông tin xác thực JWT).
    /// </param>
    /// <param name="ct">
    /// CancellationToken - NGUỒN: Controller truyền vào từ hệ thống.
    /// </param>
    /// <returns>
    /// Danh sách BookmarkResponseDto chứa các thông tin của bookmark.
    /// - BookmarkId (Int): ID định danh bookmark.
    /// - TargetType (String): Loại bookmark ("paper" hoặc "keyword").
    /// - PaperId (String?): ID bài báo (nếu là bookmark bài báo).
    /// - KeywordId (String?): ID từ khóa (nếu là bookmark từ khóa).
    /// - CreatedAt (DateTime): Thời gian tạo bookmark.
    /// </returns>
    public async Task<List<BookmarkResponseDto>> GetByUserAsync(
        int userId,
        CancellationToken ct = default)
    {
        return await _db.Bookmarks
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookmarkResponseDto
            {
                BookmarkId = b.BookmarkId,
                TargetType = b.TargetType,
                PaperId    = b.PaperId,
                KeywordId  = b.KeywordId,
                CreatedAt  = b.CreatedAt
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Thêm một bookmark mới (bài báo hoặc từ khóa) cho người dùng.
    /// </summary>
    /// <param name="userId">
    /// Số nguyên Int - NGUỒN: Controller truyền vào. ID của người dùng thực hiện bookmark.
    /// </param>
    /// <param name="dto">
    /// BookmarkRequestDto - NGUỒN: FE gửi qua Controller. Dữ liệu chứa loại bookmark và các ID tương ứng.
    /// </param>
    /// <param name="ct">
    /// CancellationToken - NGUỒN: Controller truyền vào.
    /// </param>
    /// <returns>
    /// Boolean — true nếu bookmark thành công; false nếu dữ liệu không hợp lệ hoặc đã tồn tại.
    /// </returns>
    public async Task<bool> AddAsync(
        int userId,
        BookmarkRequestDto dto,
        CancellationToken ct = default)
    {
        if (dto.TargetType == "paper" && string.IsNullOrWhiteSpace(dto.PaperId))
            return false;

        if (dto.TargetType == "keyword" && string.IsNullOrWhiteSpace(dto.KeywordId))
            return false;

        if (dto.TargetType != "paper" && dto.TargetType != "keyword")
            return false;

        // Kiểm tra đã bookmark chưa
        var exists = dto.TargetType == "paper"
            ? await _db.Bookmarks.AnyAsync(b =>
                b.UserId == userId && b.PaperId == dto.PaperId, ct)
            : await _db.Bookmarks.AnyAsync(b =>
                b.UserId == userId && b.KeywordId == dto.KeywordId, ct);

        if (exists) return false;

        _db.Bookmarks.Add(new Bookmark
        {
            UserId     = userId,
            TargetType = dto.TargetType,
            PaperId    = dto.TargetType == "paper"   ? dto.PaperId   : null,
            KeywordId  = dto.TargetType == "keyword" ? dto.KeywordId : null,
            CreatedAt  = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[Bookmark] User {UserId} bookmark {Type} {Id}",
            userId, dto.TargetType, dto.PaperId ?? dto.KeywordId);

        return true;
    }

    /// <summary>
    /// Xóa một bookmark đã lưu của người dùng dựa trên ID bookmark.
    /// </summary>
    /// <param name="userId">
    /// Số nguyên Int - NGUỒN: Controller truyền vào. ID người dùng sở hữu bookmark.
    /// </param>
    /// <param name="bookmarkId">
    /// Số nguyên Int - NGUỒN: Controller truyền vào. ID của bookmark cần xóa.
    /// </param>
    /// <param name="ct">
    /// CancellationToken - NGUỒN: Controller truyền vào.
    /// </param>
    /// <returns>
    /// Boolean — true nếu xóa thành công; false nếu không tìm thấy bookmark hoặc không có quyền sở hữu.
    /// </returns>
    public async Task<bool> RemoveAsync(
        int userId,
        int bookmarkId,
        CancellationToken ct = default)
    {
        var bookmark = await _db.Bookmarks
            .FirstOrDefaultAsync(b =>
                b.BookmarkId == bookmarkId && b.UserId == userId, ct);

        if (bookmark == null)
            return false;

        _db.Bookmarks.Remove(bookmark);
        await _db.SaveChangesAsync(ct);

        return true;
    }
}




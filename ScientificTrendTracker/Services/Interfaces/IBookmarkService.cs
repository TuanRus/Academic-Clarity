namespace ScientificTrendTracker.Services.Interfaces;

using ScientificTrendTracker.Models.DTOs.Bookmark;

/// <summary>
/// Hợp đồng nghiệp vụ cho tầng xử lý bookmark bài báo và từ khóa của người dùng.
/// </summary>
public interface IBookmarkService
{
    /// <summary>
    /// Lấy toàn bộ bookmark của một người dùng cụ thể.
    /// </summary>
    /// <param name="userId">Số nguyên Int - NGUỒN: Controller truyền vào (lấy từ JWT hoặc test).</param>
    /// <param name="ct">CancellationToken - NGUỒN: Controller truyền vào từ hệ thống.</param>
    /// <returns>Danh sách BookmarkResponseDto của người dùng. Trả về list rỗng nếu chưa có bookmark.</returns>
    Task<List<BookmarkResponseDto>> GetByUserAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Thêm một bookmark mới cho người dùng.
    /// </summary>
    /// <param name="userId">Số nguyên Int - NGUỒN: Controller truyền vào.</param>
    /// <param name="dto">BookmarkRequestDto - NGUỒN: FE gửi qua Controller. Loại bookmark và ID tương ứng.</param>
    /// <param name="ct">CancellationToken - NGUỒN: Controller truyền vào.</param>
    /// <returns>
    /// Boolean — true nếu bookmark thành công; false nếu dữ liệu không hợp lệ hoặc đã tồn tại.
    /// Controller sẽ tự gán message phù hợp dựa trên giá trị trả về.
    /// </returns>
    Task<bool> AddAsync(int userId, BookmarkRequestDto dto, CancellationToken ct = default);

    /// <summary>
    /// Xóa một bookmark đã lưu của người dùng dựa trên ID bookmark.
    /// </summary>
    /// <param name="userId">Số nguyên Int - NGUỒN: Controller truyền vào.</param>
    /// <param name="bookmarkId">Số nguyên Int - NGUỒN: Controller truyền vào.</param>
    /// <param name="ct">CancellationToken - NGUỒN: Controller truyền vào.</param>
    /// <returns>
    /// Boolean — true nếu xóa thành công; false nếu không tìm thấy bookmark.
    /// </returns>
    Task<bool> RemoveAsync(int userId, int bookmarkId, CancellationToken ct = default);
}





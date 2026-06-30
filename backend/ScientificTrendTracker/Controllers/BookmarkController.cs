namespace ScientificTrendTracker.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs.Bookmark;
using ScientificTrendTracker.Services.Interfaces;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

[ApiController]
[Route("api/bookmarks")]
[Authorize]
public class BookmarkController : ControllerBase
{
    private readonly IBookmarkService _bookmarkService;

    public BookmarkController(IBookmarkService bookmarkService)
        => _bookmarkService = bookmarkService;

    /// <summary>Lấy userId của người dùng đang đăng nhập từ JWT (claim NameIdentifier).</summary>
    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    /// <summary>
    /// Lấy danh sách toàn bộ bookmark của người dùng hiện tại (bài báo hoặc từ khóa).
    /// </summary>
    /// <param name="ct">
    /// CancellationToken - NGUỒN: ASP.NET Core runtime tự động truyền vào để hủy tác vụ khi cần.
    /// </param>
    /// <returns>
    /// Trả về đối tượng ApiResponse bọc dữ liệu danh sách các bookmark của người dùng.
    /// - isSuccess (bool): true nếu lấy dữ liệu thành công.
    /// - statusCode (int): 200 OK.
    /// - message (String): Số lượng bookmark tìm thấy.
    /// - data (Array): Danh sách BookmarkResponseDto.
    /// </returns>
    [HttpGet]
    public async Task<IActionResult> GetMyBookmarksAsync(CancellationToken ct)
    {
        var result = await _bookmarkService.GetByUserAsync(CurrentUserId, ct);
        return Ok(ApiResponse<object>.Ok(result,
            $"You have {result.Count} bookmark(s)"));
    }

    /// <summary>
    /// Thêm mới một bookmark (lưu bài báo hoặc từ khóa yêu thích) cho người dùng.
    /// </summary>
    /// <param name="dto">
    /// BookmarkRequestDto - NGUỒN: FE truyền lên qua Body (JSON). Chứa loại bookmark (paper/keyword) và ID tương ứng.
    /// </param>
    /// <param name="ct">
    /// CancellationToken - NGUỒN: ASP.NET Core runtime tự động truyền vào.
    /// </param>
    /// <returns>
    /// Trả về đối tượng ApiResponse thông báo kết quả.
    /// - isSuccess (bool): true nếu thêm thành công, false nếu thất bại hoặc đã tồn tại.
    /// - statusCode (int): 200 OK hoặc 400 Bad Request.
    /// - message (String): Thông báo kết quả chi tiết.
    /// - data (null)
    /// </returns>
    [HttpPost]
    public async Task<IActionResult> AddBookmarkAsync(
         [FromBody] BookmarkRequestDto dto,
        CancellationToken ct)
    {
        var success = await _bookmarkService.AddAsync(CurrentUserId, dto, ct);

        return success
            ? Ok(ApiResponse<object>.Ok(default, "Bookmark added successfully!"))
            : BadRequest(ApiResponse<object>.Fail(400, "Invalid data or bookmark already exists."));
    }

    /// <summary>
    /// Xóa một bookmark đã lưu của người dùng dựa trên ID bookmark.
    /// </summary>
    /// <param name="id">
    /// Số nguyên Int - NGUỒN: FE truyền lên qua Route Parameter. ID của bookmark cần xóa.
    /// </param>
    /// <param name="ct">
    /// CancellationToken - NGUỒN: ASP.NET Core runtime tự động truyền vào.
    /// </param>
    /// <returns>
    /// Trả về đối tượng ApiResponse thông báo kết quả.
    /// - isSuccess (bool): true nếu xóa thành công, false nếu không tìm thấy.
    /// - statusCode (int): 200 OK hoặc 404 Not Found.
    /// - message (String): Thông báo kết quả chi tiết.
    /// - data (null)
    /// </returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> RemoveBookmarkAsync(int id, CancellationToken ct)
    {
        var success = await _bookmarkService.RemoveAsync(CurrentUserId, id, ct);

        return success
            ? Ok(ApiResponse<object>.Ok(default, "Bookmark removed successfully!"))
            : NotFound(ApiResponse<object>.Fail(404, "Bookmark with this ID not found."));
    }
}




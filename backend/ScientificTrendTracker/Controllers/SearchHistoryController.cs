namespace ScientificTrendTracker.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs.Search;
using ScientificTrendTracker.Services.Interfaces;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

[ApiController]
[Route("api/search")]
[Authorize]
public class SearchHistoryController : ControllerBase
{
    private readonly ISearchHistoryService _service;

    public SearchHistoryController(ISearchHistoryService service)
        => _service = service;

    /// <summary>Lấy userId của người dùng đang đăng nhập từ JWT (claim NameIdentifier).</summary>
    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    /// <summary>
    /// Lưu lại một lượt tìm kiếm mới vào lịch sử của người dùng.
    /// </summary>
    /// <param name="dto">
    /// SearchHistoryRequestDto - NGUỒN: FE truyền lên qua Body (JSON). Chứa nội dung và phân loại tìm kiếm (keyword, author, doi...).
    /// </param>
    /// <param name="ct">
    /// CancellationToken - NGUỒN: ASP.NET Core runtime tự động truyền vào.
    /// </param>
    /// <returns>
    /// Trả về ApiResponse phản hồi trạng thái lưu thành công hay lỗi.
    /// - isSuccess (bool): true nếu lưu thành công, false nếu dữ liệu không hợp lệ.
    /// - statusCode (int): 200 OK hoặc 400 Bad Request.
    /// - message (String): Thông báo trạng thái.
    /// - data (null)
    /// </returns>
    [HttpPost("history")]
    public async Task<IActionResult> SaveAsync(
        [FromBody] SearchHistoryRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.SearchText))
            return BadRequest(ApiResponse<object>.Fail(400, "SearchText cannot be empty"));

        await _service.SaveAsync(CurrentUserId, dto, ct);
        return Ok(ApiResponse<object>.Ok(default, "Search history saved successfully"));
    }

    /// <summary>
    /// Lấy danh sách lịch sử tìm kiếm trước đây của người dùng hiện tại (sắp xếp mới nhất lên đầu).
    /// </summary>
    /// <param name="limit">
    /// Số nguyên Int - NGUỒN: FE truyền lên qua Query String. Giới hạn số lượng bản ghi trả về (mặc định 20).
    /// </param>
    /// <param name="ct">
    /// CancellationToken - NGUỒN: ASP.NET Core runtime tự động truyền vào.
    /// </param>
    /// <returns>
    /// Trả về ApiResponse bọc danh sách các mục lịch sử tìm kiếm.
    /// - isSuccess (bool): true.
    /// - statusCode (int): 200 OK.
    /// - message (String): Thông báo số lượng lịch sử tìm thấy.
    /// - data (Array): Mảng các đối tượng SearchHistoryResponseDto.
    /// </returns>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistoryAsync(
        [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var result = await _service.GetHistoryAsync(CurrentUserId, limit, ct);
        return Ok(ApiResponse<object>.Ok(result,
            $"Search history ({result.Count} items)"));
    }

    /// <summary>
    /// Xóa một mục lịch sử tìm kiếm cụ thể dựa vào ID.
    /// </summary>
    /// <param name="id">
    /// Số nguyên Int - NGUỒN: FE truyền lên qua Route Parameter. ID của mục lịch sử cần xóa.
    /// </param>
    /// <param name="ct">
    /// CancellationToken - NGUỒN: ASP.NET Core runtime tự động truyền vào.
    /// </param>
    /// <returns>
    /// Trả về ApiResponse phản hồi trạng thái xóa.
    /// - isSuccess (bool): true nếu xóa thành công, false nếu không tìm thấy mục.
    /// - statusCode (int): 200 OK hoặc 404 Not Found.
    /// - message (String): Kết quả xóa.
    /// - data (null)
    /// </returns>
    [HttpDelete("history/{id:int}")]
    public async Task<IActionResult> DeleteOneAsync(int id, CancellationToken ct)
    {
        var ok = await _service.DeleteOneAsync(CurrentUserId, id, ct);
        return ok
            ? Ok(ApiResponse<object>.Ok(default, "Deleted successfully"))
            : NotFound(ApiResponse<object>.Fail(404, "Not found"));
    }

    /// <summary>
    /// Xóa toàn bộ lịch sử tìm kiếm của người dùng hiện tại.
    /// </summary>
    /// <param name="ct">
    /// CancellationToken - NGUỒN: ASP.NET Core runtime tự động truyền vào.
    /// </param>
    /// <returns>
    /// Trả về ApiResponse xác nhận xóa thành công.
    /// - isSuccess (bool): true.
    /// - statusCode (int): 200 OK.
    /// - message (String): Thông báo xóa toàn bộ lịch sử thành công.
    /// - data (null)
    /// </returns>
    [HttpDelete("history")]
    public async Task<IActionResult> DeleteAllAsync(CancellationToken ct)
    {
        await _service.DeleteAllAsync(CurrentUserId, ct);
        return Ok(ApiResponse<object>.Ok(default, "All search history cleared successfully"));
    }

    /// <summary>
    /// Lấy danh sách gợi ý từ khóa tự động hoàn thành (AutoComplete) dựa theo lịch sử tìm kiếm của người dùng.
    /// </summary>
    /// <param name="q">
    /// Chuỗi String - NGUỒN: FE truyền lên qua Query String. Tiền tố từ khóa người dùng đang gõ.
    /// </param>
    /// <param name="ct">
    /// CancellationToken - NGUỒN: ASP.NET Core runtime tự động truyền vào.
    /// </param>
    /// <returns>
    /// Trả về ApiResponse bọc danh sách các từ khóa gợi ý.
    /// - isSuccess (bool): true.
    /// - statusCode (int): 200 OK.
    /// - message (String): Số lượng từ gợi ý tìm được.
    /// - data (Array): Mảng chứa đối tượng SearchSuggestionDto (bao gồm từ khóa gợi ý và số tần suất tìm kiếm).
    /// </returns>
    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestionsAsync(
        [FromQuery] string q = "", CancellationToken ct = default)
    {
        var result = await _service.GetSuggestionsAsync(CurrentUserId, q, ct);
        return Ok(ApiResponse<object>.Ok(result,
            $"{result.Count} suggestions for '{q}'"));
    }
}




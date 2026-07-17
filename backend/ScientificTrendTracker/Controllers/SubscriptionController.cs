using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs.Subscription;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Controllers;

[ApiController]
[Route("api/subscriptions")]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    /// <summary>Lấy UserId của người dùng hiện tại từ claim trong JWT. Trả về null nếu không hợp lệ.</summary>
    private int? CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    /// <summary>
    /// Kiểm tra trạng thái Premium và lấy thông tin chi tiết gói dịch vụ đang hoạt động của người dùng.
    /// </summary>
    /// <param name="ct">
    /// CancellationToken - NGUỒN: ASP.NET Core runtime tự động cung cấp để hủy yêu cầu khi cần.
    /// </param>
    /// <returns>
    /// Trả về cấu trúc ApiResponse chứa SubscriptionStatusResponseDto.
    /// - isSuccess (bool): true nếu truy vấn thành công.
    /// - statusCode (int): 200 OK.
    /// - message (String): Thông báo thành công.
    /// - data (Object): Thông tin chi tiết hạn dùng Premium.
    /// </returns>
    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> GetStatusAsync(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId == null)
            return Unauthorized(ApiResponse<SubscriptionStatusResponseDto>.Fail(401, "Invalid user identity."));

        var result = await _subscriptionService.GetSubscriptionStatusAsync(userId.Value, ct);
        return Ok(ApiResponse<SubscriptionStatusResponseDto>.Ok(result, "Premium status checked successfully."));
    }

    /// <summary>
    /// Thực hiện đăng ký hoặc gia hạn gói dịch vụ VIP/Premium cho người dùng.
    /// </summary>
    /// <param name="dto">
    /// SubscribeRequestDto - NGUỒN: FE truyền lên qua Body JSON chứa PlanId.
    /// </param>
    /// <param name="ct">
    /// CancellationToken - NGUỒN: ASP.NET Core runtime tự động cung cấp.
    /// </param>
    /// <returns>
    /// Trả về đối tượng ApiResponse thông báo kết quả đăng ký.
    /// - isSuccess (bool): true nếu đăng ký thành công, false nếu lỗi gói dịch vụ không tồn tại.
    /// - statusCode (int): 200 OK hoặc 400 Bad Request.
    /// - message (String): Kết quả chi tiết.
    /// - data (null)
    /// </returns>
    [HttpPost("subscribe")]
    [Authorize]
    public async Task<IActionResult> SubscribeAsync([FromBody] SubscribeRequestDto dto, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId == null)
            return Unauthorized(ApiResponse<object>.Fail(401, "Invalid user identity."));

        var success = await _subscriptionService.SubscribePlanAsync(userId.Value, dto, ct);

        if (!success)
        {
            return BadRequest(ApiResponse<object>.Fail(400, "Subscription failed. The plan does not exist or is locked."));
        }

        return Ok(ApiResponse<object>.Ok(null, "Plan subscribed successfully!"));
    }

    /// <summary>
    /// Admin kiểm tra trạng thái Premium và thời hạn sử dụng gói của một người dùng bất kỳ theo ID.
    /// </summary>
    /// <param name="userId">
    /// Số nguyên Int - NGUỒN: FE truyền lên qua Route Parameter. ID của người dùng cần tra cứu.
    /// </param>
    /// <param name="ct">
    /// CancellationToken - NGUỒN: ASP.NET Core runtime tự động cung cấp.
    /// </param>
    /// <returns>
    /// Trả về đối tượng ApiResponse bọc dữ liệu trạng thái Premium của người dùng được tra cứu.
    /// - isSuccess (bool): true nếu tra cứu thành công.
    /// - statusCode (int): 200 OK.
    /// - message (String): Thông báo kết quả tra cứu.
    /// - data (SubscriptionStatusResponseDto): Chứa thông tin đăng ký dịch vụ gồm:
    ///   - IsPremiumActive (bool): Trạng thái Premium có đang hoạt động hay không.
    ///   - PlanId (int?): ID của gói đăng ký hiện tại.
    ///   - PlanName (string): Tên gói dịch vụ.
    ///   - Status (string): Trạng thái gói dịch vụ.
    ///   - StartedAt (DateTime?): Ngày bắt đầu.
    ///   - EndsAt (DateTime?): Ngày hết hạn.
    /// </returns>
    [HttpGet("admin/users/{userId:int}/status")]
    public async Task<IActionResult> GetUserStatusForAdminAsync(int userId, CancellationToken ct)
    {
        var result = await _subscriptionService.GetSubscriptionStatusAsync(userId, ct);
        return Ok(ApiResponse<SubscriptionStatusResponseDto>.Ok(result, $"Subscription status of user {userId} retrieved successfully."));
    }

    /// <summary>
    /// Lấy danh sách các gói dịch vụ đang kích hoạt trong hệ thống để hiển thị bảng giá cho người dùng chọn mua.
    /// </summary>
    /// <param name="ct">
    /// CancellationToken - NGUỒN: ASP.NET Core runtime tự động cung cấp.
    /// </param>
    /// <returns>
    /// Trả về đối tượng ApiResponse bọc danh sách SubscriptionPlanDto.
    /// - isSuccess (bool): true nếu lấy dữ liệu thành công.
    /// - statusCode (int): 200 OK.
    /// - message (String): Thông báo kết quả kèm số lượng gói cước.
    /// - data (Array): Danh sách các gói cước đang hoạt động, mỗi phần tử gồm PlanId, PlanName, PriceAmount, DurationDays.
    /// </returns>
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlansAsync(CancellationToken ct)
    {
        var result = await _subscriptionService.GetActivePlansAsync(ct);
        return Ok(ApiResponse<List<SubscriptionPlanDto>>.Ok(result, $"Found {result.Count} available subscription plan(s)."));
    }
}

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Services.Interfaces;
using ScientificTrendTracker.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ScientificTrendTracker.Controllers
{
    /// <summary>
    /// Điều hướng các yêu cầu HTTP liên quan đến hành vi 
    /// Theo dõi (Follow) và Hủy theo dõi các mục học thuật.
    /// </summary>
    [Authorize] // Bắt buộc đăng nhập bảo mật (Mục 1)
    [ApiController]
    [Route("api/follows")] // Định dạng route danh từ chữ thường số nhiều
    public class FollowController : ControllerBase
    {
        private readonly IFollowService _followService;

        /// <summary>
        /// Hàm khởi tạo Controller và tiêm dịch vụ xử lý.
        /// </summary>
        /// <param name="followService">IFollowService - DI - Dịch vụ xử lý nghiệp vụ theo dõi.</param>
        public FollowController(IFollowService followService)
        {
            _followService = followService;
        }

        /// <summary>
        /// Đảo ngược trạng thái theo dõi (Bật/Tắt) cho Topic hoặc Journal.
        /// </summary>
        /// <param name="request">ToggleFollowDto - FE truyền qua Body - Thông tin mục tiêu cần đảo trạng thái.</param>
        /// <returns>
        /// ApiResponse&lt;FollowResultDto&gt;. Data gồm: IsFollowing (true nếu vừa follow, false nếu vừa unfollow), TotalFollowers (tổng số người follow hiện tại).
        /// Trả về HTTP 200 nếu thao tác thành công.
        /// Trả về HTTP 400 nếu thông tin TargetType hoặc TargetId không hợp lệ.
        /// </returns>
        [HttpPost("toggle")]
        public async Task<IActionResult> ToggleFollow(
            [FromBody] ToggleFollowDto request)
        {
            // 1. Trích xuất mã định danh UserId từ Token đã đăng nhập
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized(
                    ApiResponse<object>.Fail(
                        401,
                        "Invalid user identity."));
            }

            if (!int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(
                    ApiResponse<object>.Fail(401, "Invalid user identity."));
            }

            // 2. Gọi tầng Service xử lý thô dữ liệu nghiệp vụ (Mục 1)
            var result = await _followService.ToggleFollowAsync(
                userId,
                request);

            // 3. Controller độc quyền điều phối Http Status Code và bọc hộp (Mục 4)
            if (result == null)
            {
                // Trả về 400 nếu sai loại mục hoặc ID không tồn tại dưới DB (Mục 4)
                return BadRequest(
                    ApiResponse<object>.Fail(
                        400,
                        "Invalid item type or target ID."));
            }

            // Thao tác thành công -> Bọc ApiResponse qua static factory (Mục 6)
            return Ok(
                ApiResponse<FollowResultDto>.Ok(
                    result,
                    "Changed follow state successfully."));
        }

        /// <summary>Lấy danh sách các mục (topic/journal) người dùng hiện đang theo dõi.</summary>
        /// <returns>ApiResponse&lt;List&lt;FollowedItemDto&gt;&gt; — 200 kèm danh sách; 401 nếu token sai.</returns>
        [HttpGet]
        public async Task<IActionResult> GetMyFollows()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized(ApiResponse<object>.Fail(401, "Invalid user identity."));

            var items = await _followService.GetMyFollowsAsync(userId);
            return Ok(ApiResponse<System.Collections.Generic.List<FollowedItemDto>>.Ok(items, $"{items.Count} followed item(s)."));
        }
    }
}
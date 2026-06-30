using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Controllers
{
    /// <summary>
    /// Thông báo phía NGƯỜI DÙNG (chuông + trung tâm thông báo): xem danh sách, đếm chưa đọc, đánh dấu đã đọc.
    /// Tách khỏi NotificationController (chỉ admin/system được trigger phát thông báo).
    /// </summary>
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class MyNotificationController : ControllerBase
    {
        private readonly INotificationService _service;
        public MyNotificationController(INotificationService service) => _service = service;

        private int CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        /// <summary>GET /api/notifications/me — danh sách thông báo của user.</summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMine([FromQuery] int limit = 30)
        {
            var items = await _service.GetMyNotificationsAsync(CurrentUserId, limit);
            return Ok(ApiResponse<object>.Ok(items, $"{items.Count} thông báo."));
        }

        /// <summary>GET /api/notifications/me/unread-count — số thông báo chưa đọc (badge chuông).</summary>
        [HttpGet("me/unread-count")]
        public async Task<IActionResult> UnreadCount()
        {
            var count = await _service.GetUnreadCountAsync(CurrentUserId);
            return Ok(ApiResponse<object>.Ok(new { count }, "OK"));
        }

        /// <summary>PUT /api/notifications/me/{id}/read — đánh dấu 1 thông báo đã đọc.</summary>
        [HttpPut("me/{id:int}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var ok = await _service.MarkReadAsync(CurrentUserId, id);
            return ok
                ? Ok(ApiResponse<object>.Ok(null, "Đã đánh dấu đã đọc."))
                : NotFound(ApiResponse<object>.Fail(404, "Không tìm thấy thông báo."));
        }

        /// <summary>PUT /api/notifications/me/read-all — đánh dấu tất cả đã đọc.</summary>
        [HttpPut("me/read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            var n = await _service.MarkAllReadAsync(CurrentUserId);
            return Ok(ApiResponse<object>.Ok(new { updated = n }, $"Đã đánh dấu {n} thông báo là đã đọc."));
        }
    }
}

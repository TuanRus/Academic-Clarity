using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JournalTrend.Core.DTOs;
using JournalTrend.Services.Interfaces;
using JournalTrend.API.Hubs;

namespace JournalTrend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationController(INotificationService notificationService, IHubContext<NotificationHub> hubContext)
        {
            _notificationService = notificationService;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Tiếp nhận thông tin xuất bản bài nghiên cứu khoa học mới, găm lịch sử DB và đẩy chuông thời gian thực.
        /// </summary>
        /// <param name="trigger">NotificationTriggerDto - JSON Body - Thông tin bài báo và danh mục tạp chí liên quan.</param>
        /// <returns>
        /// ApiResponse&lt;List&lt;int&gt;&gt;. Data chứa danh sách mảng ID các User thỏa mãn nhận cảnh báo.
        /// Luôn trả về HTTP 200 (Nếu list rỗng nghĩa là luồng chạy bình thường không có ai theo dõi).
        /// </returns>
        [HttpPost("trigger-new-paper")]
        public async Task<IActionResult> TriggerNewPaper([FromBody] NotificationTriggerDto trigger)
        {
            var userIds = await _notificationService.CheckAndPushAsync(trigger);

            if (userIds.Any())
            {
                // TỐI ƯU CONCURRENCY: Thay thế luồng foreach tuần tự cũ bằng mảng Task chạy song song đồng thời, bứt phá tốc độ mạng ảo Tailscale
                var broadcastTasks = userIds.Select(userId =>
                    _hubContext.Clients.Group($"User_{userId}").SendAsync("ReceiveNotification", new
                    {
                        title = "New Publication Detected!",
                        message = $"The paper '{trigger.PaperTitle}' has been published matching your followed journals or topics."
                    }));

                await Task.WhenAll(broadcastTasks);
            }

            string customMessage = userIds.Any()
                ? $"Hệ thống đã kích nổ chuông và đẩy thông báo tới {userIds.Count} học giả."
                : "Không tìm thấy người dùng nào đăng ký theo dõi mục này.";

            return Ok(ApiResponse<List<int>>.Ok(userIds, customMessage));
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Services.Interfaces;
using ScientificTrendTracker.BackgroundServices;
using ScientificTrendTracker.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ScientificTrendTracker.Controllers
{
    /// <summary>
    /// Điều hướng các yêu cầu HTTP liên quan đến hệ thống 
    /// thông báo và kích nổ chuông real-time cho học giả.
    /// </summary>
    [ApiController]
    [Authorize(Roles = "admin,system")] // Chặn đứng rủi ro spam thông báo hàng loạt từ public client
    [Route("api/notifications")]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly IHubContext<NotificationHub> _hubContext;

        /// <summary>
        /// Hàm khởi tạo Controller và tiêm dịch vụ cùng ngữ cảnh Hub.
        /// </summary>
        /// <param name="notificationService">INotificationService - DI - Dịch vụ xử lý nghiệp vụ thông báo.</param>
        /// <param name="hubContext">IHubContext&lt;NotificationHub&gt; - DI - Ngữ cảnh SignalR Hub để đẩy chuông real-time.</param>
        public NotificationController(
            INotificationService notificationService,
            IHubContext<NotificationHub> hubContext)
        {
            _notificationService = notificationService;
            _hubContext = hubContext;
        }

        /// <summary>
        /// Tiếp nhận thông tin bài báo mới để lưu vết lịch sử 
        /// và kích nổ chuông thông báo real-time xuống client.
        /// </summary>
        /// <param name="trigger">NotificationTriggerDto - Hệ thống quét tự động truyền qua Body.</param>
        /// <returns>
        /// ApiResponse&lt;List&lt;int&gt;&gt;. Data chứa danh sách ID người dùng đã nhận tin.
        /// Trả về HTTP 200 nếu luồng lưu vết và bắn tin đồng thời hoàn tất.
        /// Trả về HTTP 400 nếu tiêu đề bài báo rỗng.
        /// </returns>
        [HttpPost("trigger-new-paper")]
        public async Task<IActionResult> TriggerNewPaper(
            [FromBody] NotificationTriggerDto trigger)
        {
            // Kiểm tra tiêu đề sớm nhằm triệt tiêu các yêu cầu rác 
            // trước khi tốn tài nguyên truy vấn xuống bảng FollowedItems.
            if (string.IsNullOrEmpty(trigger.PaperTitle))
            {
                return BadRequest(
                    ApiResponse<List<int>>.Fail(
                        400,
                        "Tiêu đề bài báo không được để trống."));
            }

            List<int> userIds = await _notificationService
                .CheckAndPushAsync(trigger);

            if (userIds.Any())
            {
                // Sử dụng cơ chế phát tin đồng thời để giải phóng luồng 
                // xử lý, đảm bảo tốc độ đẩy pop-up real-time đạt hiệu 
                // năng cao nhất mà không bị block luồng tuần tự khi số lượng follower lớn.
                var broadcastTasks = userIds.Select(userId =>
                    _hubContext.Clients
                        .Group($"User_{userId}")
                        .SendAsync(
                            "ReceiveNotification",
                            trigger.PaperTitle));

                await Task.WhenAll(broadcastTasks);
            }

            // Trả về DTO có kiểu cụ thể để Swagger tự động ánh xạ chính xác schema 
            // và Frontend không cần sử dụng các biện pháp bóc tách chuỗi thủ công.
            return Ok(
                ApiResponse<List<int>>.Ok(
                    userIds,
                    $"Đã kích nổ thành công thông báo đến {userIds.Count} học giả."));
        }
    }
}
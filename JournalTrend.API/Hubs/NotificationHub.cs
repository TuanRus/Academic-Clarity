using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace JournalTrend.API.Hubs
{
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class NotificationHub : Microsoft.AspNetCore.SignalR.Hub
    {
        /// <summary>
        /// Kích hoạt tự động ngay khi Client thiết lập kết nối thành công.
        /// Thực hiện bốc UserId từ Claim để đưa người dùng vào Group đích danh.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            // 1. Trích xuất mã định danh UserId từ thẻ bài Claims do cấu trúc Auth cấp
            string? userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            }
            await base.OnConnectedAsync();
        }
        /// <summary>
        /// Kích hoạt khi người dùng tắt trình duyệt hoặc ngắt kết nối.
        /// Hệ thống tự động xóa ConnectionId ra khỏi Group mà không cần viết thêm code.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                // Tự động dọn dẹp bộ nhớ RAM, gỡ kết nối khỏi phòng khi học giả rớt mạng
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
            }

            await base.OnDisconnectedAsync(exception);
        }

    }
}


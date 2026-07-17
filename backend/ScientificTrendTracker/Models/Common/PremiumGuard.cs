using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Models.Common
{
    /// <summary>
    /// Gate CỨNG cho các tính năng Premium: khác các endpoint "giảm chất lượng" cho Free,
    /// helper này trả 403 nếu user không có gói Premium còn hạn. Admin được miễn (BR-26).
    /// Dùng chung cho LatexController và IdeaController.
    /// </summary>
    public static class PremiumGuard
    {
        // Role claim trong JWT là RoleId dạng chuỗi số (xem AuthService); 1 = Admin.
        private const string AdminRoleId = "1";

        /// <summary>
        /// Kiểm tra quyền Premium của user hiện tại.
        /// </summary>
        /// <param name="user">ClaimsPrincipal - NGUỒN: property User của Controller.</param>
        /// <param name="subscriptionService">ISubscriptionService - NGUỒN: DI của Controller.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime.</param>
        /// <returns>null nếu được phép; ngược lại IActionResult 401/403 để Controller return ngay.</returns>
        public static async Task<IActionResult> CheckAsync(ClaimsPrincipal user, ISubscriptionService subscriptionService, CancellationToken ct)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return new UnauthorizedObjectResult(ApiResponse<object>.Fail(401, "Danh tính người dùng không hợp lệ."));

            if (user.IsInRole(AdminRoleId))
                return null;

            var status = await subscriptionService.GetSubscriptionStatusAsync(userId, ct);
            if (status == null || !status.IsPremiumActive)
                return new ObjectResult(ApiResponse<object>.Fail(403, "Tính năng dành cho tài khoản Premium. Vui lòng nâng cấp để sử dụng."))
                { StatusCode = 403 };

            return null;
        }
    }
}

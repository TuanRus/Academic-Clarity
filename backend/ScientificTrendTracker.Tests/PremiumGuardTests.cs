using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs.Subscription;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Tests
{
    /// <summary>
    /// Test PremiumGuard — gate cứng tính năng Premium:
    /// thiếu danh tính → 401, Admin → qua không cần hỏi DB, Premium active → qua, còn lại → 403.
    /// </summary>
    public class PremiumGuardTests
    {
        /// <summary>Stub subscription service: chỉ GetSubscriptionStatusAsync có nghĩa; ghi lại có bị gọi hay không.</summary>
        private sealed class StubSubscriptionService : ISubscriptionService
        {
            private readonly bool _isPremiumActive;
            public bool WasCalled;

            public StubSubscriptionService(bool isPremiumActive) => _isPremiumActive = isPremiumActive;

            public Task<SubscriptionStatusResponseDto> GetSubscriptionStatusAsync(int userId, CancellationToken ct)
            {
                WasCalled = true;
                return Task.FromResult(new SubscriptionStatusResponseDto { IsPremiumActive = _isPremiumActive });
            }

            public Task<bool> SubscribePlanAsync(int userId, SubscribeRequestDto dto, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<SubscriptionPlanDto>> GetActivePlansAsync(CancellationToken ct) => throw new NotImplementedException();
            public Task<List<AdminSubscriptionPlanDto>> GetAllPlansForAdminAsync(CancellationToken ct) => throw new NotImplementedException();
            public Task<bool> CreatePlanAsync(CreateSubscriptionPlanDto dto, CancellationToken ct) => throw new NotImplementedException();
            public Task<bool> UpdatePlanAsync(int planId, UpdateSubscriptionPlanDto dto, CancellationToken ct) => throw new NotImplementedException();
            public Task<bool> TogglePlanStatusAsync(int planId, bool isActive, CancellationToken ct) => throw new NotImplementedException();
            public Task<string> DeletePlanAsync(int planId, CancellationToken ct) => throw new NotImplementedException();
        }

        private static ClaimsPrincipal UserWith(string userId = null, string roleId = null)
        {
            var claims = new List<Claim>();
            if (userId != null) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            if (roleId != null) claims.Add(new Claim(ClaimTypes.Role, roleId));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }

        [Fact]
        public async Task ThieuClaimDanhTinh_Tra401()
        {
            var sub = new StubSubscriptionService(true);
            var result = await PremiumGuard.CheckAsync(UserWith(), sub, CancellationToken.None);

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            var body = Assert.IsType<ApiResponse<object>>(unauthorized.Value);
            Assert.Equal(401, body.StatusCode);
            Assert.False(sub.WasCalled);
        }

        [Fact]
        public async Task ClaimDanhTinhKhongPhaiSo_Tra401()
        {
            var result = await PremiumGuard.CheckAsync(UserWith("abc"), new StubSubscriptionService(true), CancellationToken.None);
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task Admin_QuaLuon_KhongGoiDb()
        {
            // BR-26: Admin (role claim "1") = auto Premium — không được đụng DB subscription.
            var sub = new StubSubscriptionService(isPremiumActive: false);
            var result = await PremiumGuard.CheckAsync(UserWith("7", roleId: "1"), sub, CancellationToken.None);

            Assert.Null(result);
            Assert.False(sub.WasCalled);
        }

        [Fact]
        public async Task PremiumConHan_Qua()
        {
            var sub = new StubSubscriptionService(isPremiumActive: true);
            var result = await PremiumGuard.CheckAsync(UserWith("7", roleId: "3"), sub, CancellationToken.None);

            Assert.Null(result);
            Assert.True(sub.WasCalled);
        }

        [Fact]
        public async Task KhongPremium_Tra403()
        {
            var sub = new StubSubscriptionService(isPremiumActive: false);
            var result = await PremiumGuard.CheckAsync(UserWith("7", roleId: "3"), sub, CancellationToken.None);

            var obj = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, obj.StatusCode);
            var body = Assert.IsType<ApiResponse<object>>(obj.Value);
            Assert.False(body.Success);
            Assert.Equal(403, body.StatusCode);
        }
    }
}

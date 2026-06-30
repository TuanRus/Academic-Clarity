using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.DTOs.Subscription;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services;

/// <summary>
/// Triển khai chi tiết các nghiệp vụ đăng ký và kiểm tra thời hạn Premium sử dụng cơ sở dữ liệu.
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(AppDbContext dbContext, ILogger<SubscriptionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Thực hiện kiểm tra ngày hết hạn so với thời gian UTC hiện tại để quyết định quyền Premium.
    /// </summary>
    public async Task<SubscriptionStatusResponseDto> GetSubscriptionStatusAsync(int userId, CancellationToken ct)
    {
        // Lấy thông tin bản ghi đăng ký mới nhất của người dùng
        var sub = await _dbContext.UserSubscriptions
            .Include(us => us.Plan)
            .Where(us => us.UserId == userId)
            .OrderByDescending(us => us.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (sub == null)
        {
            // Trả về gói Free mặc định nếu chưa đăng ký bao giờ
            return new SubscriptionStatusResponseDto
            {
                IsPremiumActive = false,
                PlanId = null,
                PlanName = "Free",
                Status = "INACTIVE",
                StartedAt = null,
                EndsAt = null
            };
        }

        // So sánh thời gian UTC hiện tại với ngày hết hạn để kiểm tra hết hạn sử dụng
        bool isExpired = sub.EndsAt.HasValue && sub.EndsAt.Value < DateTime.UtcNow;
        bool isPremiumActive = sub.Status == "ACTIVE" && !isExpired;

        return new SubscriptionStatusResponseDto
        {
            IsPremiumActive = isPremiumActive,
            PlanId = sub.PlanId,
            PlanName = sub.Plan?.PlanName ?? "Unknown",
            Status = isPremiumActive ? "ACTIVE" : (isExpired ? "EXPIRED" : sub.Status),
            StartedAt = sub.StartedAt,
            EndsAt = sub.EndsAt
        };
    }

    /// <summary>
    /// Đăng ký gói dịch vụ mới. Nếu đang có gói cũ còn hạn sẽ tự động cộng dồn thời gian.
    /// </summary>
    public async Task<bool> SubscribePlanAsync(int userId, SubscribeRequestDto dto, CancellationToken ct)
    {
        // 1. Kiểm tra gói dịch vụ có tồn tại và đang hoạt động không
        var plan = await _dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.PlanId == dto.PlanId && p.IsActive, ct);

        if (plan == null)
        {
            _logger.LogWarning("Đăng ký PlanId {PlanId} không thành công do gói không tồn tại hoặc bị dừng hoạt động.", dto.PlanId);
            return false;
        }

        var now = DateTime.UtcNow;
        var startedAt = now;

        // 2. Cộng dồn hạn sử dụng nếu gói Premium hiện tại vẫn còn hạn
        var activeSub = await _dbContext.UserSubscriptions
            .Where(us => us.UserId == userId && us.Status == "ACTIVE" && (!us.EndsAt.HasValue || us.EndsAt.Value > now))
            .OrderByDescending(us => us.EndsAt)
            .FirstOrDefaultAsync(ct);

        if (activeSub != null && activeSub.EndsAt.HasValue)
        {
            startedAt = activeSub.EndsAt.Value;
        }

        var endsAt = startedAt.AddDays(plan.DurationDays);

        // 3. Lưu thông tin đăng ký dịch vụ
        var newSub = new UserSubscription
        {
            UserId = userId,
            PlanId = plan.PlanId,
            Status = "ACTIVE",
            StartedAt = startedAt,
            EndsAt = endsAt,
            CreatedAt = now
        };

        _dbContext.UserSubscriptions.Add(newSub);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Người dùng {UserId} đăng ký thành công gói {PlanName}. Hạn sử dụng: {StartedAt} -> {EndsAt}",
            userId, plan.PlanName, startedAt, endsAt);

        return true;
    }

    /// <summary>
    /// Lấy danh sách các gói đăng ký dịch vụ đang hoạt động trong hệ thống để hiển thị bảng giá.
    /// </summary>
    public async Task<List<SubscriptionPlanDto>> GetActivePlansAsync(CancellationToken ct)
    {
        return await _dbContext.SubscriptionPlans
            .Where(p => p.IsActive)
            .Select(p => new SubscriptionPlanDto
            {
                PlanId = p.PlanId,
                PlanName = p.PlanName,
                PriceAmount = p.PriceAmount,
                DurationDays = p.DurationDays
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Admin lấy toàn bộ danh sách cấu hình các gói cước trong hệ thống.
    /// </summary>
    public async Task<List<AdminSubscriptionPlanDto>> GetAllPlansForAdminAsync(CancellationToken ct)
    {
        return await _dbContext.SubscriptionPlans
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new AdminSubscriptionPlanDto
            {
                PlanId = p.PlanId,
                PlanName = p.PlanName,
                PriceAmount = p.PriceAmount,
                DurationDays = p.DurationDays,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Admin tạo mới một gói dịch vụ.
    /// </summary>
    public async Task<bool> CreatePlanAsync(CreateSubscriptionPlanDto dto, CancellationToken ct)
    {
        bool nameExists = await _dbContext.SubscriptionPlans
            .AnyAsync(p => p.PlanName == dto.PlanName.Trim(), ct);

        if (nameExists)
        {
            _logger.LogWarning("Tạo gói cước thất bại: Tên gói '{PlanName}' đã tồn tại.", dto.PlanName);
            return false;
        }

        var plan = new SubscriptionPlan
        {
            PlanName = dto.PlanName.Trim(),
            PriceAmount = dto.PriceAmount,
            DurationDays = dto.DurationDays,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.SubscriptionPlans.Add(plan);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Admin tạo thành công gói dịch vụ mới: {PlanName} (Id: {PlanId})", plan.PlanName, plan.PlanId);
        return true;
    }

    /// <summary>
    /// Admin cập nhật cấu hình thông tin gói dịch vụ.
    /// </summary>
    public async Task<bool> UpdatePlanAsync(int planId, UpdateSubscriptionPlanDto dto, CancellationToken ct)
    {
        var plan = await _dbContext.SubscriptionPlans.FindAsync(new object[] { planId }, ct);
        if (plan == null)
        {
            _logger.LogWarning("Cập nhật gói cước thất bại: Không tìm thấy gói cước ID {PlanId}.", planId);
            return false;
        }

        bool nameDuplicate = await _dbContext.SubscriptionPlans
            .AnyAsync(p => p.PlanName == dto.PlanName.Trim() && p.PlanId != planId, ct);

        if (nameDuplicate)
        {
            _logger.LogWarning("Cập nhật gói cước thất bại: Tên gói '{PlanName}' bị trùng với gói khác.", dto.PlanName);
            return false;
        }

        plan.PlanName = dto.PlanName.Trim();
        plan.PriceAmount = dto.PriceAmount;
        plan.DurationDays = dto.DurationDays;
        plan.IsActive = dto.IsActive;
        plan.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Admin cập nhật thành công gói dịch vụ ID {PlanId} thành: {PlanName}", planId, plan.PlanName);
        return true;
    }

    /// <summary>
    /// Admin bật/tắt kích hoạt gói dịch vụ (Soft Delete).
    /// </summary>
    public async Task<bool> TogglePlanStatusAsync(int planId, bool isActive, CancellationToken ct)
    {
        var plan = await _dbContext.SubscriptionPlans.FindAsync(new object[] { planId }, ct);
        if (plan == null)
        {
            _logger.LogWarning("Bật/tắt trạng thái gói cước thất bại: Không tìm thấy gói cước ID {PlanId}.", planId);
            return false;
        }

        plan.IsActive = isActive;
        plan.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Admin thay đổi trạng thái gói dịch vụ ID {PlanId} thành: IsActive = {IsActive}", planId, isActive);
        return true;
    }

    /// <summary>
    /// Admin xoá cứng gói cước nếu chưa có UserSubscription nào tham chiếu (tránh vỡ khoá ngoại).
    /// </summary>
    public async Task<string> DeletePlanAsync(int planId, CancellationToken ct)
    {
        var plan = await _dbContext.SubscriptionPlans.FindAsync(new object[] { planId }, ct);
        if (plan == null)
        {
            _logger.LogWarning("Xoá gói cước thất bại: Không tìm thấy gói cước ID {PlanId}.", planId);
            return "NOT_FOUND";
        }

        bool inUse = await _dbContext.UserSubscriptions.AnyAsync(s => s.PlanId == planId, ct);
        if (inUse)
        {
            _logger.LogWarning("Xoá gói cước thất bại: Gói ID {PlanId} đã có người đăng ký — nên dùng Disable.", planId);
            return "IN_USE";
        }

        _dbContext.SubscriptionPlans.Remove(plan);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Admin xoá thành công gói cước ID {PlanId} ({PlanName}).", planId, plan.PlanName);
        return "OK";
    }
}

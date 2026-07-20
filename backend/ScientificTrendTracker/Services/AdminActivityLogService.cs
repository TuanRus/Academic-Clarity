using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs.ActivityLog;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services;

/// <summary>
/// Thực thi dịch vụ ghi nhận và lấy lịch sử hoạt động quản trị của Admin.
/// </summary>
public class AdminActivityLogService : IAdminActivityLogService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AdminActivityLogService> _logger;

    public AdminActivityLogService(AppDbContext dbContext, ILogger<AdminActivityLogService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Ghi nhận một hành động mới của Admin vào cơ sở dữ liệu (BEST-EFFORT).
    /// Audit log KHÔNG được làm hỏng nghiệp vụ chính: nếu AdminId chưa tồn tại trong Users
    /// (khóa ngoại) hoặc ghi lỗi thì chỉ cảnh báo rồi bỏ qua, không ném exception.
    /// </summary>
    public async Task LogActivityAsync(int adminId, string action, string description, string ipAddress)
    {
        // Lấy email admin THẬT từ DB (đồng thời xác nhận AdminId tồn tại để không vỡ FK).
        var adminEmail = await _dbContext.Users
            .Where(u => u.UserId == adminId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();

        if (adminEmail == null)
        {
            _logger.LogWarning("Bỏ ghi AdminActivityLog: AdminId {AdminId} không tồn tại trong Users (action={Action}).", adminId, action);
            return;
        }

        var log = new AdminActivityLog
        {
            AdminId = adminId,
            AdminEmail = adminEmail,
            Action = action,
            Description = description,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _dbContext.AdminActivityLogs.Add(log);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _dbContext.Entry(log).State = EntityState.Detached; // gỡ khỏi tracker để không kẹt SaveChanges sau
            _logger.LogWarning(ex, "Ghi AdminActivityLog thất bại (action={Action}) — bỏ qua để không ảnh hưởng nghiệp vụ.", action);
        }
    }

    /// <summary>
    /// Lấy danh sách nhật ký thao tác Admin có phân trang và sắp xếp mới nhất.
    /// </summary>
    public async Task<PagedResult<AdminActivityLogResponseDto>> GetActivityLogsAsync(int page, int pageSize, CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var query = _dbContext.AdminActivityLogs
            .OrderByDescending(al => al.CreatedAt);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(al => new AdminActivityLogResponseDto
            {
                LogId = al.LogId,
                AdminId = al.AdminId,
                AdminEmail = al.AdminEmail,
                AdminName = "System Admin", // Tên hiển thị mặc định
                Action = al.Action,
                Description = al.Description,
                IpAddress = al.IpAddress,
                CreatedAt = al.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<AdminActivityLogResponseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}

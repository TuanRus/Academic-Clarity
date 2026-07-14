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

    public AdminActivityLogService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Ghi nhận một hành động mới của Admin vào cơ sở dữ liệu.
    /// </summary>
    public async Task LogActivityAsync(int adminId, string action, string description, string ipAddress)
    {
        // Tạm thời gán email giả định theo ID (do bảng users chưa được cấu hình).
        // Sau này khi có cơ chế Auth: bạn có thể lấy thẳng Email/Name từ Claims của JWT Token truyền vào.
        string adminEmail = adminId == 1 ? "admin_test@scientifictrend.com" : $"admin_{adminId}@scientifictrend.com";

        var log = new AdminActivityLog
        {
            AdminId = adminId,
            AdminEmail = adminEmail,
            Action = action,
            Description = description,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AdminActivityLogs.Add(log);
        await _dbContext.SaveChangesAsync();
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

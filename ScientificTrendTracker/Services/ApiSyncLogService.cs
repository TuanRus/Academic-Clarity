using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs.SyncLog;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services;

/// <summary>
/// Thực thi dịch vụ lấy thông tin nhật ký đồng bộ dữ liệu hệ thống.
/// </summary>
public class ApiSyncLogService : IApiSyncLogService
{
    private readonly AppDbContext _dbContext;

    public ApiSyncLogService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Lấy danh sách lịch sử đồng bộ phân trang, sắp xếp từ mới nhất đến cũ nhất.
    /// </summary>
    public async Task<PagedResult<ApiSyncLogResponseDto>> GetSyncLogsAsync(int page, int pageSize, CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var query = _dbContext.ApiSyncLogs
            .Include(sl => sl.DataSource)
            .OrderByDescending(sl => sl.SyncStartedAt);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(sl => new ApiSyncLogResponseDto
            {
                SyncLogId = sl.SyncLogId,
                SourceName = sl.DataSource != null ? sl.DataSource.SourceName : "Unknown",
                SyncStartedAt = sl.SyncStartedAt,
                SyncFinishedAt = sl.SyncFinishedAt,
                Status = sl.Status,
                RecordsImported = sl.RecordsImported,
                ErrorMessage = sl.ErrorMessage
            })
            .ToListAsync(ct);

        return new PagedResult<ApiSyncLogResponseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}

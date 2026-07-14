using System.Threading;
using System.Threading.Tasks;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs.SyncLog;

namespace ScientificTrendTracker.Services.Interfaces;

/// <summary>
/// Giao diện dịch vụ truy vấn nhật ký đồng bộ hệ thống.
/// </summary>
public interface IApiSyncLogService
{
    /// <summary>
    /// Lấy danh sách nhật ký đồng bộ dữ liệu hệ thống phân trang.
    /// </summary>
    Task<PagedResult<ApiSyncLogResponseDto>> GetSyncLogsAsync(int page, int pageSize, CancellationToken ct);
}

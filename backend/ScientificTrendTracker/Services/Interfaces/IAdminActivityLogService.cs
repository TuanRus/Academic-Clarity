using System.Threading;
using System.Threading.Tasks;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs.ActivityLog;

namespace ScientificTrendTracker.Services.Interfaces;

/// <summary>
/// Giao diện dịch vụ ghi nhận và truy vấn nhật ký hoạt động của Admin.
/// </summary>
public interface IAdminActivityLogService
{
    /// <summary>
    /// Ghi nhận một hành động của Admin vào nhật ký hệ thống.
    /// </summary>
    Task LogActivityAsync(int adminId, string action, string description, string ipAddress);

    /// <summary>
    /// Lấy danh sách lịch sử thao tác của các Admin dạng phân trang.
    /// </summary>
    Task<PagedResult<AdminActivityLogResponseDto>> GetActivityLogsAsync(int page, int pageSize, CancellationToken ct);
}

using System;

namespace ScientificTrendTracker.Models.DTOs.ActivityLog;

/// <summary>
/// DTO chứa thông tin nhật ký hoạt động/thao tác của Admin hiển thị lên Web.
/// </summary>
public class AdminActivityLogResponseDto
{
    public int LogId { get; set; }
    public int AdminId { get; set; }
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

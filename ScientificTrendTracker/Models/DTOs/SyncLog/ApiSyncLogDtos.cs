using System;

namespace ScientificTrendTracker.Models.DTOs.SyncLog;

/// <summary>
/// DTO chứa thông tin nhật ký đồng bộ dữ liệu hệ thống hiển thị cho Admin.
/// </summary>
public class ApiSyncLogResponseDto
{
    public int SyncLogId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public DateTime SyncStartedAt { get; set; }
    public DateTime? SyncFinishedAt { get; set; }
    public string Status { get; set; } = "running";
    public int RecordsImported { get; set; }
    public string? ErrorMessage { get; set; }
}

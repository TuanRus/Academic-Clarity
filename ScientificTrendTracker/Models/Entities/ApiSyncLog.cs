using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScientificTrendTracker.Models.Entities;

/// <summary>
/// Thực thể ghi nhận nhật ký chi tiết các phiên chạy tác vụ đồng bộ ngầm của hệ thống.
/// </summary>
[Table("ApiSyncLogs")]
public class ApiSyncLog
{
    [Key]
    public int SyncLogId { get; set; }

    [Required]
    public int DataSourceId { get; set; }
    public ApiDataSource? DataSource { get; set; }

    public DateTime SyncStartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SyncFinishedAt { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "running"; // running, success, failed

    public int RecordsImported { get; set; }

    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }
}

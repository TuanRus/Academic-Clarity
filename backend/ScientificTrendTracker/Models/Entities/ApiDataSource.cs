using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScientificTrendTracker.Models.Entities;

/// <summary>
/// Thực thể đại diện cho cấu hình các nguồn dữ liệu đồng bộ (ví dụ: OpenAlex).
/// </summary>
[Table("ApiDataSources")]
public class ApiDataSource
{
    [Key]
    public int DataSourceId { get; set; }

    [Required]
    [MaxLength(100)]
    public string SourceName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string BaseUrl { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [Required]
    [MaxLength(50)]
    public string SyncFrequency { get; set; } = "weekly";

    public DateTime? LastSyncAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScientificTrendTracker.Models.Entities;

/// <summary>
/// Thực thể ghi lại nhật ký lịch sử các hành động thay đổi dữ liệu của quản trị viên (Admin).
/// </summary>
[Table("AdminActivityLogs")]
public class AdminActivityLog
{
    [Key]
    public int LogId { get; set; }

    [Required]
    public int AdminId { get; set; }

    [Required]
    [MaxLength(255)]
    public string AdminEmail { get; set; } = string.Empty; // Lưu trực tiếp email để truy vết độc lập và an toàn khi query

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty; // RESET_KEYWORDS, CREATE_PLAN, etc.

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string IpAddress { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

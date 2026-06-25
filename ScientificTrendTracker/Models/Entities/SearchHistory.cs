using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScientificTrendTracker.Models.Entities;

/// <summary>
/// Lưu trữ lịch sử các từ khóa tìm kiếm của người dùng.
/// </summary>
[Table("SearchHistories")]
public class SearchHistory
{
    [Key]
    public int SearchHistoryId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [MaxLength(500)]
    public string SearchText { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string SearchType { get; set; } = string.Empty;

    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
}





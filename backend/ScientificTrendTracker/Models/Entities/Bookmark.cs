using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScientificTrendTracker.Models.Entities;

/// <summary>
/// Thực thể lưu trữ các bài báo hoặc từ khóa được người dùng lưu lại (Bookmark).
/// </summary>
[Table("Bookmarks")]
public class Bookmark
{
    [Key]
    public int BookmarkId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [MaxLength(20)]
    public string TargetType { get; set; } = string.Empty; // "paper" hoặc "keyword"
    
    [MaxLength(50)]
    public string? PaperId { get; set; }
    public ResearchPaper? Paper { get; set; }

    [MaxLength(100)]
    public string? KeywordId { get; set; }
    public Keyword? Keyword { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}





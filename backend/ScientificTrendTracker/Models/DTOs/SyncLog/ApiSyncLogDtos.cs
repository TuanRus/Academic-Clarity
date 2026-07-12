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

/// <summary>1 bài báo được thêm trong khung thời gian của một lần sync (dùng cho nút Detail).</summary>
public class SyncedPaperDto
{
    public string PaperId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int? PublicationYear { get; set; }
    public string? OpenAlexId { get; set; }
    public string? SourceUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

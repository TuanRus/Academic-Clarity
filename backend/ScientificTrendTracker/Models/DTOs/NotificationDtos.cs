using System.Collections.Generic;

namespace ScientificTrendTracker.Models.DTOs
{
    /// <summary>Gói dữ liệu tiếp nhận tín hiệu kích nổ sự kiện có bài báo khoa học mới phát hành.</summary>
    public class NotificationTriggerDto
    {
        /// <summary>Mã định danh duy nhất của bài báo khoa học vừa xuất bản.</summary>
        public string PaperId { get; set; } = null!;

        /// <summary>Tiêu đề của bài nghiên cứu khoa học mới.</summary>
        public string PaperTitle { get; set; } = null!;

        /// <summary>Mã định danh tạp chí khoa học phát hành bài báo này (nếu có).</summary>
        public string? JournalId { get; set; }

        /// <summary>Danh sách các mã chủ đề chuyên mục nghiên cứu gắn liền với bài báo.</summary>
        public List<string> TopicIds { get; set; } = new List<string>();
    }

    /// <summary>Một thông báo của người dùng (cho chuông + trung tâm thông báo).</summary>
    public record NotificationItemDto
    {
        public int NotificationId { get; init; }
        public string Title { get; init; } = null!;
        public string Message { get; init; } = null!;
        public string? RelatedPaperId { get; init; }
        public bool IsRead { get; init; }
        public System.DateTime CreatedAt { get; init; }
    }
}
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JournalTrend.Core.Entities
{
    /// <summary>
    /// Thực thể lưu trữ lịch sử và trạng thái theo dõi (Follow) các chuyên mục hoặc tạp chí của học giả.
    /// </summary>
    [Table("Followed_items")]
    public class FollowedItem
    {
        /// <summary>
        /// Mã định danh duy nhất của bản ghi theo dõi (Khóa chính tự tăng).
        /// </summary>
        [Key]
        [Column("follow_id")]
        public int FollowId { get; set; }

        /// <summary>
        /// Mã định danh của người dùng thực hiện hành vi theo dõi.
        /// </summary>
        [Column("user_id")]
        public int UserId { get; set; }

        /// <summary>
        /// Chuỗi định vị loại thực thể được theo dõi ('topic' hoặc 'journal').
        /// </summary>
        [Column("target_type")]
        [Required]
        [MaxLength(50)]
        public string TargetType { get; set; } = null!;

        /// <summary>
        /// Mã định danh của chủ đề nghiên cứu (Nếu theo dõi chuyên mục). Khóa ngoại trỏ đến bảng ResearchTopics.
        /// </summary>
        [Column("topic_id")]
        [MaxLength(50)]
        public string? TopicId { get; set; }

        /// <summary>
        /// Mã định danh của tạp chí khoa học (Nếu theo dõi tạp chí). Khóa ngoại trỏ đến bảng Journals.
        /// </summary>
        [Column("journal_id")]
        [MaxLength(50)]
        public string? JournalId { get; set; }

        /// <summary>
        /// Thời điểm người dùng nhấn theo dõi (Tính theo giờ UTC)[cite: 151].
                    /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ==========================================
        // NAVIGATION PROPERTIES (THUỘC TÍNH ĐIỀU HƯỚNG)
        // ==========================================

        /// <summary>
        /// Thông tin chi tiết của người dùng thực hiện theo dõi.
        /// </summary>
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        /// <summary>
        /// Thông tin chi tiết của chủ đề nghiên cứu được liên kết (Nếu có).
        /// </summary>
        [ForeignKey("TopicId")]
        public virtual ResearchTopic? ResearchTopic { get; set; }

        /// <summary>
        /// Thông tin chi tiết của tạp chí khoa học được liên kết (Nếu có).
        /// </summary>
        [ForeignKey("JournalId")]
        public virtual Journal? Journal { get; set; }
    }
}
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScientificTrendTracker.Models.Entities
{
    /// <summary>
    /// Thực thể lưu trữ thông tin thông báo gửi tới người dùng.
    /// </summary>
    [Table("Notifications")]
    public class Notification
    {
        /// <summary>
        /// Mã định danh duy nhất của thông báo (Khóa chính tự tăng).
        /// </summary>
        [Key]
        [Column("notification_id")]
        public int NotificationId { get; set; }

        /// <summary>
        /// Mã định danh của người dùng nhận thông báo.
        /// </summary>
        [Column("user_id")]
        public int UserId { get; set; }

        /// <summary>
        /// Mã định danh của đối tượng được theo dõi liên quan trực tiếp đến thông báo này (nếu có).
        /// </summary>
        [Column("followed_item_id")]
        public int? FollowedItemId { get; set; }

        /// <summary>
        /// Tiêu đề của thông báo.
        /// </summary>
        [Column("title")]
        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = null!;

        /// <summary>
        /// Nội dung chi tiết của thông báo hiển thị cho người dùng.
        /// </summary>
        [Column("message")]
        [Required]
        [MaxLength(1000)] // Khống chế độ dài tin nhắn hiển thị chuông pop-up
        public string Message { get; set; } = null!;

        /// <summary>
        /// Mã định danh của bài báo nghiên cứu khoa học liên quan trực tiếp đến thông báo (nếu có).
        /// </summary>
        [Column("related_paper_id")]
        [MaxLength(50)]
        public string? RelatedPaperId { get; set; }

        /// <summary>
        /// Trạng thái đã đọc của thông báo (true nếu đã đọc).
        /// </summary>
        [Column("is_read")]
        public bool IsRead { get; set; } = false;

        /// <summary>
        /// Thời điểm thông báo được khởi tạo (Giờ UTC).
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Thời điểm người dùng thực hiện đọc thông báo (Giờ UTC, null nếu chưa đọc).
        /// </summary>
        [Column("read_at")]
        public DateTime? ReadAt { get; set; }
    }
}
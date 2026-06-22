using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JournalTrend.Core.Entities
{
    [Table("Notifications")]
    public class Notification
    {
        [Key]
        [Column("notification_id")]
        public int NotificationId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("followed_item_id")]
        public int? FollowedItemId { get; set; }

        [Column("title")]
        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = null!;

        [Column("message")]
        [Required]
        [MaxLength(1000)] // Khống chế độ dài tin nhắn hiển thị chuông pop-up
        public string Message { get; set; } = null!;

        [Column("related_paper_id")]
        [MaxLength(50)]
        public string? RelatedPaperId { get; set; }

        [Column("is_read")]
        public bool IsRead { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("read_at")]
        public DateTime? ReadAt { get; set; }
    }
}
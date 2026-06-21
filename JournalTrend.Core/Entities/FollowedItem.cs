using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JournalTrend.Core.Entities
{
    [Table("followed_items")]
    public class FollowedItem
    {
        [Key]
        [Column("follow_id")]
        public int FollowId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("target_type")]
        [Required]
        [MaxLength(50)] // Khống chế chuỗi định vị mục 'topic' hoặc 'journal'
        public string TargetType { get; set; } = null!;

        [Column("topic_id")]
        [MaxLength(50)]
        public string? TopicId { get; set; }

        [Column("journal_id")]
        [MaxLength(50)]
        public string? JournalId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
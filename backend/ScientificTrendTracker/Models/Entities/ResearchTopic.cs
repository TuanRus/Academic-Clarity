using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScientificTrendTracker.Models.Entities
{
    /// <summary>
    /// Thực thể đại diện cho bảng lưu trữ thông tin các chủ đề nghiên cứu khoa học (Research Topics).
    /// </summary>
    [Table("ResearchTopics")]
    public class ResearchTopic
    {
        /// <summary>
        /// Mã định danh duy nhất của chủ đề nghiên cứu (Khóa chính).
        /// </summary>
        [Key]
        [Column("TopicId")]
        [MaxLength(50)]
        public string TopicId { get; set; } = null!;

        /// <summary>
        /// Tên của chủ đề nghiên cứu khoa học.
        /// </summary>
        [Column("TopicName")]
        [Required]
        [MaxLength(200)]
        public string TopicName { get; set; } = null!;

        /// <summary>
        /// Mô tả chi tiết về phạm vi hoặc nội dung của chủ đề nghiên cứu.
        /// </summary>
        [Column("Description")]
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Thời điểm bản ghi được khởi tạo trong hệ thống (Tính theo giờ UTC)[cite: 151].
        /// </summary>
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
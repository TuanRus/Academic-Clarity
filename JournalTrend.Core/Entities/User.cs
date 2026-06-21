using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JournalTrend.Core.Entities
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("email")]
        [Required]
        [MaxLength(255)] // Ép về VARCHAR(255) để làm Unique Index bảo mật
        public string Email { get; set; } = null!;

        [Column("password_hash")]
        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = null!;

        [Column("full_name")]
        [Required]
        [MaxLength(150)]
        public string Fullname { get; set; } = null!;

        [Column("account_tag")] // [NOTE: Đồng bộ với cờ cắm thẻ tag BIT/Boolean ưu đãi học thuật]
        public bool AccountTag { get; set; } = false;

        [Column("role_id")]
        public int RoleId { get; set; }
        // Sẽ nhận các giá trị phân quyền hệ thống: 1-Admin, 2-Lecturer, 3-Student, 4-Regular User

        [Column("institution")]
        [MaxLength(255)]
        public string? Institution { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreateAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("last_login_at")]
        public DateTime? LastLoginAt { get; set; }
    }
}
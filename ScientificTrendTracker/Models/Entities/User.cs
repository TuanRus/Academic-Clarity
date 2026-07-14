using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScientificTrendTracker.Models.Entities
{
    /// <summary>
    /// Thực thể đại diện cho thông tin tài khoản người dùng trong hệ thống.
    /// </summary>
    [Table("Users")]
    public class User
    {
        /// <summary>
        /// Mã định danh duy nhất của người dùng (Khóa chính tự tăng).
        /// </summary>
        [Key]
        [Column("user_id")]
        public int UserId { get; set; }

        /// <summary>
        /// Địa chỉ Email dùng để đăng nhập và nhận thông tin xác thực.
        /// </summary>
        [Column("email")]
        [Required]
        [MaxLength(255)] // Ép về VARCHAR(255) để làm Unique Index bảo mật
        public string Email { get; set; } = null!;

        /// <summary>
        /// Chuỗi băm mật khẩu đã được mã hóa bằng thuật toán BCrypt.
        /// </summary>
        [Column("password_hash")]
        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = null!;

        /// <summary>
        /// Họ và tên đầy đủ của người dùng.
        /// </summary>
        [Column("full_name")]
        [Required]
        [MaxLength(150)]
        public string Fullname { get; set; } = null!;

        /// <summary>
        /// Cờ đánh dấu tài khoản thuộc diện học thuật ưu đãi (BIT/Boolean).
        /// </summary>
        [Column("account_tag")] // [NOTE: Đồng bộ với cờ cắm thẻ tag BIT/Boolean ưu đãi học thuật]
        public bool AccountTag { get; set; } = false;

        /// <summary>
        /// Mã định danh vai trò của người dùng (1-Admin, 2-Lecturer, 3-Student, 4-Regular User).
        /// </summary>
        [Column("role_id")]
        public int RoleId { get; set; }
        // Sẽ nhận các giá trị phân quyền hệ thống: 1-Admin, 2-Lecturer, 3-Student, 4-Regular User

        /// <summary>
        /// Tên cơ quan, tổ chức nghiên cứu hoặc trường học công tác.
        /// </summary>
        [Column("institution")]
        [MaxLength(255)]
        public string? Institution { get; set; }

        /// <summary>
        /// Cờ trạng thái hoạt động của tài khoản.
        /// </summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Thời điểm tài khoản được khởi tạo (Giờ UTC).
        /// </summary>
        [Column("created_at")]
        public DateTime CreateAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Thời điểm thông tin tài khoản được cập nhật lần cuối (Giờ UTC).
        /// </summary>
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Thời điểm đăng nhập hệ thống lần cuối cùng (Giờ UTC).
        /// </summary>
        [Column("last_login_at")]
        public DateTime? LastLoginAt { get; set; }
    }
}
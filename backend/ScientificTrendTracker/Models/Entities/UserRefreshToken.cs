using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScientificTrendTracker.Models.Entities
{
    /// <summary>
    /// Thực thể đại diện cho mã Refresh Token dùng để duy trì và xoay vòng phiên đăng nhập của người dùng.
    /// </summary>
    [Table("UserRefreshTokens")]
    public class UserRefreshToken
    {
        /// <summary>
        /// Mã định danh duy nhất của Token (Khóa chính kiểu Guid).
        /// </summary>
        [Key]
        [Column("token_id")]
        public Guid TokenId { get; set; }

        /// <summary>
        /// Mã định danh của người dùng sở hữu Token.
        /// </summary>
        [Column("user_id")]
        public int UserId { get; set; }

        /// <summary>
        /// Chuỗi băm bảo mật của Refresh Token.
        /// </summary>
        [Column("token_hash")]
        [Required]
        [MaxLength(255)] // Giới hạn chuỗi băm tránh phình to DB
        public string TokenHash { get; set; } = null!;

        /// <summary>
        /// Trạng thái đã bị hủy bỏ/thu hồi của Token (true nếu đã bị thu hồi).
        /// </summary>
        [Column("is_revoked")]
        public bool IsRevoked { get; set; } = false;

        /// <summary>
        /// Thời điểm Token hết hạn sử dụng (Giờ UTC).
        /// </summary>
        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Thời điểm khởi tạo Token (Giờ UTC).
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Đối tượng thực thể người dùng liên kết với token này (Navigation property).
        /// </summary>
        // Mối quan hệ ngoại vi đảo chiều (Foreign Key) kết nối về gốc User
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        /// <summary>
        /// ID duy nhất của JWT được liên kết với Refresh Token này.
        /// </summary>
        [Column("jwt_id")]
        [Required]
        [MaxLength(255)] // Gắn Index quét đích danh token phiên
        public string JwtId { get; set; } = null!;

        /// <summary>
        /// Thời điểm Token bị thu hồi (Giờ UTC, null nếu chưa bị thu hồi).
        /// </summary>
        [Column("revoked_at")]
        public DateTime? RevokedAt { get; set; }
    }
}
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JournalTrend.Core.Entities
{
    [Table("user_refresh_tokens")]
    public class UserRefreshToken
    {
        [Key]
        [Column("token_id")]
        public Guid TokenId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("token_hash")]
        [Required]
        [MaxLength(255)] // Giới hạn chuỗi băm tránh phình to DB
        public string TokenHash { get; set; } = null!;

        [Column("is_revoked")]
        public bool IsRevoked { get; set; } = false;

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Mối quan hệ ngoại vi đảo chiều (Foreign Key) kết nối về gốc User
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Column("jwt_id")]
        [Required]
        [MaxLength(255)] // Gắn Index quét đích danh token phiên
        public string JwtId { get; set; } = null!;

        [Column("revoked_at")]
        public DateTime? RevokedAt { get; set; }
    }
}
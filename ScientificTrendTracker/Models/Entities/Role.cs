using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScientificTrendTracker.Models.Entities
{
    /// <summary>
    /// Thực thể đại diện cho vai trò phân quyền của người dùng trong hệ thống (như Admin, Lecturer, Student...).
    /// </summary>
    [Table("Roles")]
    public class Role
    {
        /// <summary>
        /// Mã định danh duy nhất của vai trò (Khóa chính).
        /// </summary>
        [Key]
        [Column("role_id")]
        public int RoleId { get; set; }

        /// <summary>
        /// Tên vai trò (Ví dụ: admin, student...).
        /// </summary>
        [Column("role_name")]
        [Required]
        [MaxLength(50)]
        public string RoleName { get; set; } = null!;

        /// <summary>
        /// Mô tả chi tiết về quyền hạn hoặc ý nghĩa của vai trò.
        /// </summary>
        [Column("description")]
        [MaxLength(255)]
        public string? Description { get; set; }
    }
}
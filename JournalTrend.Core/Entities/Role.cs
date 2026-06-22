using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JournalTrend.Core.Entities
{
    [Table("Roles")]
    public class Role
    {
        [Key]
        [Column("role_id")]
        public int RoleId { get; set; }

        [Column("role_name")]
        [Required]
        [MaxLength(50)]
        public string RoleName { get; set; } = null!;

        [Column("description")]
        [MaxLength(255)]
        public string? Description { get; set; }
    }
}
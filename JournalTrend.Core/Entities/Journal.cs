using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JournalTrend.Core.Entities
{
    /// <summary>
    /// Thực thể đại diện cho bảng lưu trữ thông tin các tạp chí khoa học (Journals).
    /// </summary>
    [Table("Journals")]
    public class Journal
    {
        /// <summary>
        /// Mã định danh duy nhất của tạp chí khoa học (Khóa chính).
        /// </summary>
        [Key]
        [Column("JournalId")]
        [MaxLength(50)]
        public string JournalId { get; set; } = null!;

        /// <summary>
        /// Mã định danh của tạp chí trên hệ thống OpenAlex.
        /// </summary>
        [Column("OpenAlexId")]
        [MaxLength(100)]
        public string? OpenAlexId { get; set; }

        /// <summary>
        /// Tên chính thức của tạp chí khoa học.
        /// </summary>
        [Column("JournalName")]
        [Required]
        [MaxLength(255)]
        public string JournalName { get; set; } = null!;

        /// <summary>
        /// Mã chuẩn quốc tế cho xuất bản ấn phẩm bản in (ISSN Print).
        /// </summary>
        [Column("IssnPrint")]
        [MaxLength(20)]
        public string? IssnPrint { get; set; }

        /// <summary>
        /// Mã chuẩn quốc tế cho xuất bản ấn phẩm điện tử (ISSN Electronic).
        /// </summary>
        [Column("IssnElectronic")]
        [MaxLength(20)]
        public string? IssnElectronic { get; set; }

        /// <summary>
        /// Nhà xuất bản của tạp chí khoa học.
        /// </summary>
        [Column("Publisher")]
        [MaxLength(255)]
        public string? Publisher { get; set; }

        /// <summary>
        /// Lĩnh vực nghiên cứu chính của tạp chí.
        /// </summary>
        [Column("FieldOfStudy")]
        [MaxLength(155)]
        public string? FieldOfStudy { get; set; }

        /// <summary>
        /// Cơ sở dữ liệu lập chỉ mục lưu trữ tạp chí (như Scopus, WoS...).
        /// </summary>
        [Column("IndexingDatabase")]
        [MaxLength(100)]
        public string? IndexingDatabase { get; set; }

        /// <summary>
        /// Phân hạng phân vị của tạp chí (như Q1, Q2, Q3, Q4).
        /// </summary>
        [Column("QuartileRank")]
        [MaxLength(10)]
        public string? QuartileRank { get; set; }

        /// <summary>
        /// Năm thực hiện xếp hạng chỉ số của tạp chí.
        /// </summary>
        [Column("RankingYear")]
        public int? RankingYear { get; set; }

        /// <summary>
        /// Chỉ số ảnh hưởng (Impact Factor) của tạp chí khoa học.
        /// </summary>
        [Column("ImpactFactor")]
        public decimal? ImpactFactor { get; set; }

        /// <summary>
        /// Chỉ số H-Index đo lường năng suất và mức độ tác động của các công bố.
        /// </summary>
        [Column("HIndex")]
        public int? HIndex { get; set; }

        /// <summary>
        /// Thời điểm bản ghi được khởi tạo trong hệ thống (Tính theo giờ UTC)[cite: 151].
                    /// </summary>
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
using System;
using System.ComponentModel.DataAnnotations;

namespace ScientificTrendTracker.Models.DTOs.Keyword
{
    /// <summary>
    /// DTO hiển thị danh sách từ khóa trong trang quản trị dành cho Admin.
    /// </summary>
    public class KeywordAdminDto
    {
        /// <summary>
        /// Mã định danh từ khóa (khóa chính).
        /// </summary>
        public string KeywordId { get; set; }

        /// <summary>
        /// Tên từ khóa khoa học.
        /// </summary>
        public string KeywordName { get; set; }

        /// <summary>
        /// Số lượng bài báo khoa học liên kết với từ khóa này.
        /// </summary>
        public int AssociatedPapersCount { get; set; }

        /// <summary>
        /// Ngày tạo từ khóa.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO nhận thông tin khi tạo mới một từ khóa.
    /// </summary>
    public class CreateKeywordDto
    {
        /// <summary>
        /// Tên từ khóa mới (bắt buộc).
        /// </summary>
        [Required(ErrorMessage = "Tên từ khóa không được để trống.")]
        [MaxLength(150, ErrorMessage = "Tên từ khóa không được vượt quá 150 ký tự.")]
        public string KeywordName { get; set; }
    }

    /// <summary>
    /// DTO nhận thông tin khi cập nhật tên của từ khóa.
    /// </summary>
    public class UpdateKeywordDto
    {
        /// <summary>
        /// Tên từ khóa được cập nhật (bắt buộc).
        /// </summary>
        [Required(ErrorMessage = "Tên từ khóa không được để trống.")]
        [MaxLength(150, ErrorMessage = "Tên từ khóa không được vượt quá 150 ký tự.")]
        public string KeywordName { get; set; }
    }
}

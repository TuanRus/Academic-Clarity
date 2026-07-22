using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ScientificTrendTracker.Models.DTOs.Paper
{
    /// <summary>
    /// DTO hiển thị thông tin bài báo trong danh sách quản trị dành cho Admin.
    /// </summary>
    public class PaperAdminDto
    {
        /// <summary>
        /// Mã định danh bài báo (khóa chính).
        /// </summary>
        public string PaperId { get; set; }

        /// <summary>
        /// Tiêu đề của bài báo khoa học.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Mã định danh tài liệu số (DOI).
        /// </summary>
        public string Doi { get; set; }

        /// <summary>
        /// Danh sách tên các tác giả bài báo (phân tách dấu phẩy).
        /// </summary>
        public string Authors { get; set; }

        /// <summary>
        /// Năm xuất bản bài báo.
        /// </summary>
        public int? PublicationYear { get; set; }

        /// <summary>
        /// Số lượng trích dẫn hiện tại.
        /// </summary>
        public int CitationCount { get; set; }

        /// <summary>
        /// Tên tạp chí khoa học xuất bản bài báo.
        /// </summary>
        public string JournalName { get; set; }

        /// <summary>
        /// Ngày tạo bản ghi trên hệ thống.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO hiển thị thông tin chi tiết của bài báo bao gồm các liên kết tác giả và từ khóa.
    /// </summary>
    public class PaperDetailDto
    {
        /// <summary>
        /// Mã định danh bài báo.
        /// </summary>
        public string PaperId { get; set; }

        /// <summary>
        /// ID bài báo trên hệ thống OpenAlex.
        /// </summary>
        public string OpenAlexId { get; set; }

        /// <summary>
        /// Mã định danh tài liệu số (DOI).
        /// </summary>
        public string Doi { get; set; }

        /// <summary>
        /// Tiêu đề bài báo khoa học.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Năm xuất bản.
        /// </summary>
        public int? PublicationYear { get; set; }

        /// <summary>
        /// Ngày xuất bản chính xác.
        /// </summary>
        public DateTime? PublicationDate { get; set; }

        /// <summary>
        /// Mã định danh tạp chí.
        /// </summary>
        public string JournalId { get; set; }

        /// <summary>
        /// Tên tạp chí.
        /// </summary>
        public string JournalName { get; set; }

        /// <summary>
        /// Số lượng trích dẫn.
        /// </summary>
        public int CitationCount { get; set; }

        /// <summary>
        /// Đường dẫn nguồn bài báo.
        /// </summary>
        public string SourceUrl { get; set; }

        /// <summary>
        /// Trạng thái đã xử lý trích xuất từ khóa AI hay chưa.
        /// </summary>
        public bool IsAiProcessed { get; set; }

        /// <summary>
        /// Ngày tạo bản ghi.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Ngày cập nhật bản ghi gần nhất.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Danh sách tên các tác giả của bài báo.
        /// </summary>
        public List<string> Authors { get; set; } = new();

        /// <summary>
        /// Danh sách các từ khóa liên quan đến bài báo.
        /// </summary>
        public List<string> Keywords { get; set; } = new();
    }

    /// <summary>
    /// DTO nhận thông tin khi tạo mới một bài báo.
    /// </summary>
    public class CreatePaperDto
    {
        /// <summary>
        /// Tiêu đề bài báo (bắt buộc).
        /// </summary>
        [Required(ErrorMessage = "Paper title cannot be empty.")]
        [MaxLength(500, ErrorMessage = "Title cannot exceed 500 characters.")]
        public string Title { get; set; }

        /// <summary>
        /// Mã định danh tài liệu số DOI.
        /// </summary>
        [MaxLength(255)]
        public string Doi { get; set; }

        /// <summary>
        /// Năm xuất bản bài báo.
        /// </summary>
        public int? PublicationYear { get; set; }

        /// <summary>
        /// Ngày xuất bản chính xác.
        /// </summary>
        public DateTime? PublicationDate { get; set; }

        /// <summary>
        /// Số lượng trích dẫn hiện tại của bài báo (mặc định = 0).
        /// </summary>
        public int CitationCount { get; set; } = 0;

        /// <summary>
        /// Đường dẫn nguồn bài báo.
        /// </summary>
        [MaxLength(500)]
        public string SourceUrl { get; set; }

        /// <summary>
        /// Mã OpenAlex (dạng "W..."). Cần thiết để màn Paper Detail enrich abstract/topic/institutions/open-access
        /// on-demand giống bài sync. Thiếu field này thì bài thêm-từ-link sẽ hiện "Missing data".
        /// </summary>
        [MaxLength(50)]
        public string OpenAlexId { get; set; }

        /// <summary>
        /// Chủ đề chính của bài báo (ResearchTopic). Dùng để thông báo cho follower đang theo dõi topic.
        /// </summary>
        [MaxLength(255)]
        public string Topic { get; set; }

        /// <summary>
        /// Mã định danh tạp chí (nếu chọn tạp chí sẵn).
        /// </summary>
        [MaxLength(50)]
        public string JournalId { get; set; }

        /// <summary>
        /// Tên tạp chí khoa học (nếu muốn tạo tạp chí mới).
        /// </summary>
        [MaxLength(255)]
        public string JournalName { get; set; }

        /// <summary>
        /// Danh sách tên các tác giả của bài báo.
        /// </summary>
        public List<string> Authors { get; set; } = new();

        /// <summary>
        /// Danh sách các từ khóa gán cho bài báo.
        /// </summary>
        public List<string> Keywords { get; set; } = new();
    }

    /// <summary>
    /// DTO cho luồng Admin thêm bài báo tự động bằng cách dán link (OpenAlex / DOI).
    /// </summary>
    public class CreatePaperFromLinkDto
    {
        /// <summary>
        /// Link bài báo: URL OpenAlex ("https://openalex.org/W..."), URL DOI ("https://doi.org/10..."),
        /// ID work ("W...") hoặc DOI trần ("10.xxxx/...").
        /// </summary>
        [Required(ErrorMessage = "Paper link cannot be empty.")]
        [MaxLength(500, ErrorMessage = "Link cannot exceed 500 characters.")]
        public string Link { get; set; }
    }

    /// <summary>
    /// DTO nhận thông tin khi cập nhật một bài báo.
    /// </summary>
    public class UpdatePaperDto
    {
        /// <summary>
        /// Tiêu đề bài báo (bắt buộc).
        /// </summary>
        [Required(ErrorMessage = "Paper title cannot be empty.")]
        [MaxLength(500, ErrorMessage = "Title cannot exceed 500 characters.")]
        public string Title { get; set; }

        /// <summary>
        /// Mã định danh tài liệu số DOI.
        /// </summary>
        [MaxLength(255)]
        public string Doi { get; set; }

        /// <summary>
        /// Năm xuất bản bài báo.
        /// </summary>
        public int? PublicationYear { get; set; }

        /// <summary>
        /// Ngày xuất bản chính xác.
        /// </summary>
        public DateTime? PublicationDate { get; set; }

        /// <summary>
        /// Đường dẫn nguồn bài báo.
        /// </summary>
        [MaxLength(500)]
        public string SourceUrl { get; set; }

        /// <summary>
        /// Mã định danh tạp chí.
        /// </summary>
        [MaxLength(50)]
        public string JournalId { get; set; }

        /// <summary>
        /// Tên tạp chí khoa học (nếu muốn thay đổi/tạo tạp chí mới).
        /// </summary>
        [MaxLength(255)]
        public string JournalName { get; set; }

        /// <summary>
        /// Danh sách tên các tác giả mới của bài báo (thay thế danh sách cũ).
        /// </summary>
        public List<string> Authors { get; set; } = new();

        /// <summary>
        /// Danh sách các từ khóa mới gán cho bài báo (thay thế danh sách cũ).
        /// </summary>
        public List<string> Keywords { get; set; } = new();
    }
}

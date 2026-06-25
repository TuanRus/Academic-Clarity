using System.Threading;
using System.Threading.Tasks;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs.Keyword;

namespace ScientificTrendTracker.Services.Interfaces
{
    /// <summary>
    /// Định nghĩa các nghiệp vụ CRUD quản lý từ khóa khoa học dành cho Admin.
    /// </summary>
    public interface IKeywordService
    {
        /// <summary>
        /// Lấy danh sách từ khóa có phân trang, hỗ trợ tìm kiếm dành cho Admin.
        /// </summary>
        /// <param name="search">Chuỗi String - NGUỒN: FE truyền qua Query - Tìm kiếm theo tên từ khóa.</param>
        /// <param name="page">Số nguyên Int - NGUỒN: FE truyền lên - Số trang hiện tại.</param>
        /// <param name="pageSize">Số nguyên Int - NGUỒN: FE truyền lên - Số lượng dòng trên một trang.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime cung cấp.</param>
        /// <returns>Trả về đối tượng PagedResult bọc danh sách các KeywordAdminDto.</returns>
        Task<PagedResult<KeywordAdminDto>> GetKeywordsForAdminAsync(string search, int page, int pageSize, CancellationToken ct);

        /// <summary>
        /// Thêm mới một từ khóa vào hệ thống và kiểm tra trùng tên.
        /// </summary>
        /// <param name="dto">CreateKeywordDto - NGUỒN: FE truyền qua Body request chứa tên từ khóa.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime cung cấp.</param>
        /// <returns>Trả về True nếu tạo thành công, False nếu tên từ khóa đã tồn tại.</returns>
        Task<bool> CreateKeywordAsync(CreateKeywordDto dto, CancellationToken ct);

        /// <summary>
        /// Cập nhật tên của một từ khóa hiện có.
        /// </summary>
        /// <param name="keywordId">Chuỗi String - NGUỒN: FE truyền qua URL - ID của từ khóa cần cập nhật.</param>
        /// <param name="dto">UpdateKeywordDto - NGUỒN: FE truyền qua Body request.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime cung cấp.</param>
        /// <returns>Trả về True nếu cập nhật thành công, False nếu không tìm thấy hoặc tên mới trùng với từ khóa khác.</returns>
        Task<bool> UpdateKeywordAsync(string keywordId, UpdateKeywordDto dto, CancellationToken ct);

        /// <summary>
        /// Xóa một từ khóa khỏi hệ thống, dọn dẹp các liên kết trung gian trong bảng PaperKeywords.
        /// </summary>
        /// <param name="keywordId">Chuỗi String - NGUỒN: FE truyền qua URL - ID của từ khóa cần xóa.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime cung cấp.</param>
        /// <returns>Trả về True nếu xóa thành công, False nếu từ khóa không tồn tại.</returns>
        Task<bool> DeleteKeywordAsync(string keywordId, CancellationToken ct);
    }
}

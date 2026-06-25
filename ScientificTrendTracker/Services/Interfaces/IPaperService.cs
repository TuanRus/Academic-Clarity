using System.Threading;
using System.Threading.Tasks;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs.Paper;

namespace ScientificTrendTracker.Services.Interfaces
{
    /// <summary>
    /// Định nghĩa các nghiệp vụ CRUD quản lý bài báo khoa học dành cho Admin.
    /// </summary>
    public interface IPaperService
    {
        /// <summary>
        /// Tìm kiếm và lọc danh sách bài báo khoa học với định dạng phân trang dành cho Admin.
        /// </summary>
        /// <param name="search">Chuỗi String - NGUỒN: FE truyền lên - Từ khóa tìm kiếm theo tiêu đề bài báo.</param>
        /// <param name="year">Số nguyên Nullable Int - NGUỒN: FE truyền lên - Lọc theo năm xuất bản.</param>
        /// <param name="journalId">Chuỗi String - NGUỒN: FE truyền lên - Lọc theo mã tạp chí.</param>
        /// <param name="page">Số nguyên Int - NGUỒN: FE truyền lên - Trang hiện tại cần lấy.</param>
        /// <param name="pageSize">Số nguyên Int - NGUỒN: FE truyền lên - Số lượng dòng trên một trang.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime cung cấp để hủy yêu cầu khi cần.</param>
        /// <returns>Trả về một đối tượng PagedResult bọc danh sách các PaperAdminDto.</returns>
        Task<PagedResult<PaperAdminDto>> GetPapersForAdminAsync(string search, int? year, string journalId, int page, int pageSize, CancellationToken ct);

        /// <summary>
        /// Lấy chi tiết thông tin một bài báo bao gồm các tác giả và các từ khóa liên quan.
        /// </summary>
        /// <param name="paperId">Chuỗi String - NGUỒN: FE truyền qua URL - ID của bài báo cần lấy chi tiết.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime cung cấp.</param>
        /// <returns>Trả về PaperDetailDto chứa thông tin chi tiết bài báo, hoặc null nếu không tìm thấy.</returns>
        Task<PaperDetailDto> GetPaperDetailAsync(string paperId, CancellationToken ct);

        /// <summary>
        /// Admin tự tay thêm mới một bài báo hay vào hệ thống. Tự tạo mới Tác giả/Từ khóa nếu chưa tồn tại.
        /// </summary>
        /// <param name="dto">CreatePaperDto - NGUỒN: FE truyền qua Body request chứa thông tin bài báo.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime cung cấp.</param>
        /// <returns>Trả về True nếu thêm thành công, False nếu tiêu đề bị trùng lặp hoặc không hợp lệ.</returns>
        Task<bool> CreatePaperAsync(CreatePaperDto dto, CancellationToken ct);

        /// <summary>
        /// Admin cập nhật thông tin chi tiết và danh sách liên kết tác giả/từ khóa của một bài báo.
        /// </summary>
        /// <param name="paperId">Chuỗi String - NGUỒN: FE truyền qua URL - ID bài báo cần cập nhật.</param>
        /// <param name="dto">UpdatePaperDto - NGUỒN: FE truyền qua Body request.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime cung cấp.</param>
        /// <returns>Trả về True nếu cập nhật thành công, False nếu không tìm thấy bài báo hoặc tên bài báo mới trùng với bài khác.</returns>
        Task<bool> UpdatePaperAsync(string paperId, UpdatePaperDto dto, CancellationToken ct);

        /// <summary>
        /// Admin thực hiện xóa một bài báo khỏi hệ thống. Tự động dọn dẹp các bảng liên kết trung gian.
        /// </summary>
        /// <param name="paperId">Chuỗi String - NGUỒN: FE truyền qua URL - ID bài báo cần xóa.</param>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime cung cấp.</param>
        /// <returns>Trả về True nếu xóa thành công, False nếu bài báo không tồn tại.</returns>
        Task<bool> DeletePaperAsync(string paperId, CancellationToken ct);
    }
}

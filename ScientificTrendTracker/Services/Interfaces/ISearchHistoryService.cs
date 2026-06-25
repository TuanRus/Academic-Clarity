namespace ScientificTrendTracker.Services.Interfaces;

using ScientificTrendTracker.Models.DTOs.Search;

/// <summary>
/// Hợp đồng nghiệp vụ cho tầng lưu lịch sử tìm kiếm và cung cấp gợi ý AutoComplete.
/// </summary>
public interface ISearchHistoryService
{
    /// <summary>
    /// Lưu một lượt tìm kiếm mới vào lịch sử của người dùng.
    /// </summary>
    /// <param name="userId">Số nguyên Int - NGUỒN: Controller truyền vào.</param>
    /// <param name="dto">SearchHistoryRequestDto - NGUỒN: FE gửi qua Controller.</param>
    /// <param name="ct">CancellationToken - NGUỒN: Controller truyền vào.</param>
    Task SaveAsync(int userId, SearchHistoryRequestDto dto, CancellationToken ct = default);

    /// <summary>
    /// Lấy lịch sử tìm kiếm của người dùng, sắp xếp mới nhất lên đầu.
    /// </summary>
    /// <param name="userId">Số nguyên Int - NGUỒN: Controller truyền vào.</param>
    /// <param name="limit">Số nguyên Int - NGUỒN: FE truyền qua Controller. Giới hạn số bản ghi (mặc định 20).</param>
    /// <param name="ct">CancellationToken - NGUỒN: Controller truyền vào.</param>
    /// <returns>Danh sách SearchHistoryResponseDto. Trả về list rỗng nếu chưa có lịch sử.</returns>
    Task<List<SearchHistoryResponseDto>> GetHistoryAsync(int userId, int limit = 20, CancellationToken ct = default);

    /// <summary>
    /// Xóa một mục lịch sử tìm kiếm cụ thể theo ID.
    /// </summary>
    /// <param name="userId">Số nguyên Int - NGUỒN: Controller truyền vào.</param>
    /// <param name="historyId">Số nguyên Int - NGUỒN: Controller truyền vào.</param>
    /// <param name="ct">CancellationToken - NGUỒN: Controller truyền vào.</param>
    /// <returns>Boolean — true nếu xóa thành công; false nếu không tìm thấy.</returns>
    Task<bool> DeleteOneAsync(int userId, int historyId, CancellationToken ct = default);

    /// <summary>
    /// Xóa toàn bộ lịch sử tìm kiếm của người dùng.
    /// </summary>
    /// <param name="userId">Số nguyên Int - NGUỒN: Controller truyền vào.</param>
    /// <param name="ct">CancellationToken - NGUỒN: Controller truyền vào.</param>
    Task DeleteAllAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Gợi ý từ khóa AutoComplete dựa trên lịch sử tìm kiếm của người dùng.
    /// </summary>
    /// <param name="userId">Số nguyên Int - NGUỒN: Controller truyền vào.</param>
    /// <param name="prefix">Chuỗi String - NGUỒN: FE truyền qua Controller. Tiền tố từ khóa đang gõ.</param>
    /// <param name="ct">CancellationToken - NGUỒN: Controller truyền vào.</param>
    /// <returns>Danh sách SearchSuggestionDto top 5 từ khóa hay tìm nhất. Trả về list rỗng nếu prefix rỗng.</returns>
    Task<List<SearchSuggestionDto>> GetSuggestionsAsync(int userId, string prefix, CancellationToken ct = default);
}





using ScientificTrendTracker.Models.DTOs;

namespace ScientificTrendTracker.Services.Interfaces
{
    /// <summary>
    /// Dữ liệu cho Trend Dashboard: time-series + bảng xếp hạng top, theo 3 chiều
    /// keyword / author / journal, lọc theo năm (và tháng khi groupBy=month).
    /// Chỉ số trend dùng TẦN SUẤT TƯƠNG ĐỐI (share = bài chứa entity / tổng bài kỳ đó).
    /// </summary>
    public interface ITrendService
    {
        /// <summary>
        /// Chuỗi thời gian trend của 1 entity (share theo từng kỳ) + độ dốc + hướng.
        /// </summary>
        /// <param name="dimension">"keyword" / "author" / "journal".</param>
        /// <param name="value">Tên entity cần tra (keyword exact, author/journal contains).</param>
        /// <param name="fromYear">Năm bắt đầu (mặc định 2022).</param>
        /// <param name="toYear">Năm kết thúc (mặc định 2026).</param>
        /// <param name="groupBy">"year" (mặc định) hoặc "month".</param>
        /// <returns>TrendSeriesDto, hoặc null nếu entity không tồn tại.</returns>
        Task<TrendSeriesDto> GetSeriesAsync(string dimension, string value, int fromYear, int toYear, string groupBy);

        /// <summary>
        /// Bảng xếp hạng top entity theo share trong khoảng năm.
        /// </summary>
        /// <param name="dimension">"keyword" / "author" / "journal".</param>
        /// <param name="fromYear">Năm bắt đầu.</param>
        /// <param name="toYear">Năm kết thúc.</param>
        /// <param name="topN">Số entity trả về.</param>
        /// <param name="minPapers">Bỏ entity có số bài &lt; ngần này (giảm nhiễu).</param>
        /// <param name="sortBy">"share" (mặc định) / "rising" (đang lên nhất) / "falling" (đang xuống nhất).</param>
        Task<List<TrendTopItemDto>> GetTopAsync(string dimension, int fromYear, int toYear, int topN, int minPapers, string sortBy);
    }
}

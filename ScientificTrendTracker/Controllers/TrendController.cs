using Microsoft.AspNetCore.Mvc;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Controllers
{
    /// <summary>
    /// API cho Trend Dashboard: time-series + bảng xếp hạng top, theo chiều keyword/author/journal,
    /// lọc theo năm (và tháng khi groupBy=month). Chỉ số trend = share (tần suất tương đối).
    /// </summary>
    [ApiController]
    [Route("api/trend")]
    public class TrendController : ControllerBase
    {
        private static readonly string[] ValidDimensions = { "keyword", "author", "journal" };

        private readonly ITrendService _trendService;

        public TrendController(ITrendService trendService)
        {
            _trendService = trendService;
        }

        /// <summary>
        /// Time-series trend của 1 entity (keyword/author/journal): share theo từng kỳ + độ dốc + hướng.
        /// FE vẽ line chart "đang lên / xuống" và cho lọc năm, xem theo năm hoặc tháng.
        /// </summary>
        /// <param name="dimension">
        /// string - FE truyền qua query (?dimension=) - "keyword" / "author" / "journal".
        /// </param>
        /// <param name="value">
        /// string - FE truyền qua query (?value=) - Tên entity (keyword khớp chính xác, author/journal contains).
        /// </param>
        /// <param name="fromYear">int - FE truyền qua query (?fromYear=2022) - Năm bắt đầu, mặc định 2022.</param>
        /// <param name="toYear">int - FE truyền qua query (?toYear=2026) - Năm kết thúc, mặc định 2026.</param>
        /// <param name="groupBy">
        /// string - FE truyền qua query (?groupBy=year) - "year" (mặc định) hoặc "month".
        /// </param>
        /// <returns>
        /// Chuỗi JSON (ApiResponse&lt;TrendSeriesDto&gt;) gửi cho FE. Thuộc tính Data bao gồm:
        /// - Dimension (string), Value (string), GroupBy (string): thông tin truy vấn.
        /// - TotalPapers (int): tổng bài chứa entity trong khoảng lọc.
        /// - Series (Array): mỗi điểm gồm Period ("2024" hoặc "2024-03"), Count, PeriodTotal, Share.
        /// - Slope (double), Direction (string "rising"/"falling"/"stable").
        /// Trả 400 nếu thiếu tham số/dimension sai, 404 nếu entity không tồn tại.
        /// </returns>
        [HttpGet("series")]
        public async Task<IActionResult> GetSeries(
            [FromQuery] string dimension,
            [FromQuery] string value,
            [FromQuery] int fromYear = 2022,
            [FromQuery] int toYear = 0,
            [FromQuery] string groupBy = "year")
        {
            if (toYear <= 0) toYear = DateTime.UtcNow.Year; // mặc định = năm hiện tại (không hardcode)
            if (!IsValidDimension(dimension))
                return BadRequest(ApiResponse<object>.Fail(400, "dimension phải là keyword / author / journal."));
            if (string.IsNullOrWhiteSpace(value))
                return BadRequest(ApiResponse<object>.Fail(400, "value không được để trống."));
            if (fromYear > toYear)
                return BadRequest(ApiResponse<object>.Fail(400, "fromYear không được lớn hơn toYear."));

            var series = await _trendService.GetSeriesAsync(dimension.Trim(), value.Trim(), fromYear, toYear, groupBy);

            if (series == null)
                return NotFound(ApiResponse<object>.Fail(404,
                    $"Không tìm thấy {dimension} '{value}' trong hệ thống."));

            return Ok(ApiResponse<TrendSeriesDto>.Ok(series,
                $"Trend {dimension} '{series.Value}': {series.Direction}, {series.Series.Count} kỳ."));
        }

        /// <summary>
        /// Bảng xếp hạng TOP entity (keyword/author/journal) theo share trong khoảng năm — widget "đang hot".
        /// </summary>
        /// <param name="dimension">
        /// string - FE truyền qua query (?dimension=) - "keyword" / "author" / "journal".
        /// </param>
        /// <param name="fromYear">int - FE truyền qua query (?fromYear=2022) - Năm bắt đầu, mặc định 2022.</param>
        /// <param name="toYear">int - FE truyền qua query (?toYear=2026) - Năm kết thúc, mặc định 2026.</param>
        /// <param name="topN">int - FE truyền qua query (?topN=20) - Số entity trả về, mặc định 20.</param>
        /// <param name="minPapers">
        /// int - FE truyền qua query (?minPapers=3) - Bỏ entity có số bài &lt; ngần này (giảm nhiễu), mặc định 3.
        /// </param>
        /// <param name="sortBy">
        /// string - FE truyền qua query (?sortBy=share) - "share" (mặc định, nhiều bài nhất) /
        /// "rising" (đang lên mạnh nhất) / "falling" (đang xuống mạnh nhất).
        /// </param>
        /// <returns>
        /// Chuỗi JSON (ApiResponse&lt;List&lt;TrendTopItemDto&gt;&gt;) gửi cho FE. Mỗi item gồm:
        /// - Name (string): tên entity.
        /// - Count (int): số bài chứa entity trong khoảng.
        /// - RangeTotal (int): tổng bài trong khoảng.
        /// - Share (double): Count / RangeTotal.
        /// - Slope (double), Direction (string "rising"/"falling"/"stable"): xu hướng tăng/giảm.
        /// Trả 400 nếu dimension sai hoặc khoảng năm sai.
        /// </returns>
        [HttpGet("top")]
        public async Task<IActionResult> GetTop(
            [FromQuery] string dimension,
            [FromQuery] int fromYear = 2022,
            [FromQuery] int toYear = 0,
            [FromQuery] int topN = 20,
            [FromQuery] int minPapers = 3,
            [FromQuery] string sortBy = "share")
        {
            if (toYear <= 0) toYear = DateTime.UtcNow.Year; // mặc định = năm hiện tại (không hardcode)
            if (!IsValidDimension(dimension))
                return BadRequest(ApiResponse<object>.Fail(400, "dimension phải là keyword / author / journal."));
            if (fromYear > toYear)
                return BadRequest(ApiResponse<object>.Fail(400, "fromYear không được lớn hơn toYear."));
            if (topN < 1 || topN > 100)
                return BadRequest(ApiResponse<object>.Fail(400, "topN phải từ 1 đến 100."));

            var items = await _trendService.GetTopAsync(dimension.Trim(), fromYear, toYear, topN, minPapers, sortBy);

            return Ok(ApiResponse<List<TrendTopItemDto>>.Ok(items,
                $"Top {items.Count} {dimension} từ {fromYear}–{toYear}."));
        }

        private static bool IsValidDimension(string d) =>
            !string.IsNullOrWhiteSpace(d) && ValidDimensions.Contains(d.Trim().ToLower());
    }
}

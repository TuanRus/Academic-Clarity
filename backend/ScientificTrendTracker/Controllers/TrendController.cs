using Microsoft.AspNetCore.Mvc;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly ISubscriptionService _subscriptionService;

        public TrendController(ITrendService trendService, ISubscriptionService subscriptionService)
        {
            _trendService = trendService;
            _subscriptionService = subscriptionService;
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
                return BadRequest(ApiResponse<object>.Fail(400, "dimension must be keyword / author / journal."));
            if (string.IsNullOrWhiteSpace(value))
                return BadRequest(ApiResponse<object>.Fail(400, "value cannot be empty."));
            if (fromYear > toYear)
                return BadRequest(ApiResponse<object>.Fail(400, "fromYear cannot be greater than toYear."));

            var series = await _trendService.GetSeriesAsync(dimension.Trim(), value.Trim(), fromYear, toYear, groupBy);

            if (series == null)
                return NotFound(ApiResponse<object>.Fail(404,
                    $"Could not find {dimension} '{value}' in the system."));

            return Ok(ApiResponse<TrendSeriesDto>.Ok(series,
                $"Trend for {dimension} '{series.Value}': {series.Direction}, {series.Series.Count} period(s)."));
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
                return BadRequest(ApiResponse<object>.Fail(400, "dimension must be keyword / author / journal."));
            if (fromYear > toYear)
                return BadRequest(ApiResponse<object>.Fail(400, "fromYear cannot be greater than toYear."));
            if (topN < 1 || topN > 100)
                return BadRequest(ApiResponse<object>.Fail(400, "topN must be between 1 and 100."));

            var items = await _trendService.GetTopAsync(dimension.Trim(), fromYear, toYear, topN, minPapers, sortBy);

            return Ok(ApiResponse<List<TrendTopItemDto>>.Ok(items,
                $"Top {items.Count} {dimension} from {fromYear} to {toYear}."));
        }

        /// <summary>
        /// Lấy danh sách bài báo tiêu biểu thúc đẩy xu hướng của một từ khóa cụ thể.
        /// Tài khoản thường (Free) chỉ lấy tối đa 2 bài báo. Tài khoản Premium lấy tối đa 20 bài báo.
        /// </summary>
        /// <param name="keyword">string - FE truyền qua query (?keyword=) - Từ khóa cần phân tích.</param>
        /// <param name="fromYear">int - FE truyền qua query (?fromYear=2022) - Năm bắt đầu, mặc định 2022.</param>
        /// <param name="toYear">int - FE truyền qua query (?toYear=2026) - Năm kết thúc, mặc định năm hiện tại.</param>
        /// <param name="ct">CancellationToken - ASP.NET Core tự động truyền vào.</param>
        /// <returns>
        /// ApiResponse chứa danh sách TrendPremiumPaperDto.
        /// Các HTTP status trả về:
        /// - 200 OK: Trả về thành công dữ liệu giới hạn theo loại tài khoản.
        /// - 400 Bad Request: Nếu từ khóa rỗng hoặc khoảng năm sai.
        /// - 401 Unauthorized: Nếu chưa đăng nhập.
        /// </returns>
        [Authorize]
        [HttpGet("keyword/papers")]
        public async Task<IActionResult> GetKeywordPapersAsync(
            [FromQuery] string keyword,
            [FromQuery] int fromYear = 2022,
            [FromQuery] int toYear = 0,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return BadRequest(ApiResponse<object>.Fail(400, "Keyword cannot be empty."));
            if (toYear <= 0) toYear = DateTime.UtcNow.Year;
            if (fromYear > toYear)
                return BadRequest(ApiResponse<object>.Fail(400, "fromYear cannot be greater than toYear."));

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.Fail(401, "Invalid identity."));
            }

            var subStatus = await _subscriptionService.GetSubscriptionStatusAsync(userId, ct);
            bool isPremium = subStatus.IsPremiumActive;
            int limit = isPremium ? 20 : 2;

            var papers = await _trendService.GetTopPapersForKeywordAsync(keyword.Trim(), fromYear, toYear, limit, ct);

            var message = isPremium 
                ? $"Premium Account: Found {papers.Count} representative paper(s)." 
                : $"Free Account: Showing only 2 most featured papers. Upgrade to Premium to see the full Top 20.";

            return Ok(ApiResponse<List<TrendPremiumPaperDto>>.Ok(papers, message));
        }

        /// <summary>
        /// Lấy danh sách các tác giả nghiên cứu nhiều nhất về một từ khóa cụ thể.
        /// Tài khoản thường (Free) chỉ lấy tối đa 2 tác giả. Tài khoản Premium lấy tối đa 20 tác giả.
        /// </summary>
        /// <param name="keyword">string - FE truyền qua query (?keyword=) - Từ khóa cần phân tích.</param>
        /// <param name="fromYear">int - FE truyền qua query (?fromYear=2022) - Năm bắt đầu, mặc định 2022.</param>
        /// <param name="toYear">int - FE truyền qua query (?toYear=2026) - Năm kết thúc, mặc định năm hiện tại.</param>
        /// <param name="ct">CancellationToken - ASP.NET Core tự động truyền vào.</param>
        /// <returns>
        /// ApiResponse chứa danh sách TrendPremiumAuthorDto.
        /// Các HTTP status trả về:
        /// - 200 OK: Trả về thành công dữ liệu giới hạn theo loại tài khoản.
        /// - 400 Bad Request: Nếu từ khóa rỗng hoặc khoảng năm sai.
        /// - 401 Unauthorized: Nếu chưa đăng nhập.
        /// </returns>
        [Authorize]
        [HttpGet("keyword/authors")]
        public async Task<IActionResult> GetKeywordAuthorsAsync(
            [FromQuery] string keyword,
            [FromQuery] int fromYear = 2022,
            [FromQuery] int toYear = 0,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return BadRequest(ApiResponse<object>.Fail(400, "Keyword cannot be empty."));
            if (toYear <= 0) toYear = DateTime.UtcNow.Year;
            if (fromYear > toYear)
                return BadRequest(ApiResponse<object>.Fail(400, "fromYear cannot be greater than toYear."));

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.Fail(401, "Invalid identity."));
            }

            var subStatus = await _subscriptionService.GetSubscriptionStatusAsync(userId, ct);
            bool isPremium = subStatus.IsPremiumActive;
            int limit = isPremium ? 20 : 2;

            var authors = await _trendService.GetTopAuthorsForKeywordAsync(keyword.Trim(), fromYear, toYear, limit, ct);

            var message = isPremium 
                ? $"Premium Account: Found {authors.Count} top author(s)." 
                : $"Free Account: Showing only 2 most featured authors. Upgrade to Premium to see the full Top 20.";

            return Ok(ApiResponse<List<TrendPremiumAuthorDto>>.Ok(authors, message));
        }

        /// <summary>
        /// Lấy danh sách các tạp chí xuất bản nhiều nhất về một từ khóa cụ thể.
        /// Tài khoản thường (Free) chỉ lấy tối đa 2 tạp chí. Tài khoản Premium lấy tối đa 20 tạp chí.
        /// </summary>
        /// <param name="keyword">string - FE truyền qua query (?keyword=) - Từ khóa cần phân tích.</param>
        /// <param name="fromYear">int - FE truyền qua query (?fromYear=2022) - Năm bắt đầu, mặc định 2022.</param>
        /// <param name="toYear">int - FE truyền qua query (?toYear=2026) - Năm kết thúc, mặc định năm hiện tại.</param>
        /// <param name="ct">CancellationToken - ASP.NET Core tự động truyền vào.</param>
        /// <returns>
        /// ApiResponse chứa danh sách TrendPremiumJournalDto.
        /// Các HTTP status trả về:
        /// - 200 OK: Trả về thành công dữ liệu giới hạn theo loại tài khoản.
        /// - 400 Bad Request: Nếu từ khóa rỗng hoặc khoảng năm sai.
        /// - 401 Unauthorized: Nếu chưa đăng nhập.
        /// </returns>
        [Authorize]
        [HttpGet("keyword/journals")]
        public async Task<IActionResult> GetKeywordJournalsAsync(
            [FromQuery] string keyword,
            [FromQuery] int fromYear = 2022,
            [FromQuery] int toYear = 0,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return BadRequest(ApiResponse<object>.Fail(400, "Keyword cannot be empty."));
            if (toYear <= 0) toYear = DateTime.UtcNow.Year;
            if (fromYear > toYear)
                return BadRequest(ApiResponse<object>.Fail(400, "fromYear cannot be greater than toYear."));

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.Fail(401, "Invalid identity."));
            }

            var subStatus = await _subscriptionService.GetSubscriptionStatusAsync(userId, ct);
            bool isPremium = subStatus.IsPremiumActive;
            int limit = isPremium ? 20 : 2;

            var journals = await _trendService.GetTopJournalsForKeywordAsync(keyword.Trim(), fromYear, toYear, limit, ct);

            var message = isPremium 
                ? $"Premium Account: Found {journals.Count} top journal(s)." 
                : $"Free Account: Showing only 2 most featured journals. Upgrade to Premium to see the full Top 20.";

            return Ok(ApiResponse<List<TrendPremiumJournalDto>>.Ok(journals, message));
        }

        /// <summary>
        /// Lấy danh sách các từ khóa thường đồng xuất hiện với từ khóa mục tiêu.
        /// Tài khoản thường (Free) chỉ lấy tối đa 2 từ khóa đồng xuất hiện. Tài khoản Premium lấy tối đa 20 từ khóa đồng xuất hiện.
        /// </summary>
        /// <param name="keyword">string - FE truyền qua query (?keyword=) - Từ khóa cần phân tích.</param>
        /// <param name="fromYear">int - FE truyền qua query (?fromYear=2022) - Năm bắt đầu, mặc định 2022.</param>
        /// <param name="toYear">int - FE truyền qua query (?toYear=2026) - Năm kết thúc, mặc định năm hiện tại.</param>
        /// <param name="ct">CancellationToken - ASP.NET Core tự động truyền vào.</param>
        /// <returns>
        /// ApiResponse chứa danh sách CoOccurringKeywordDto.
        /// Các HTTP status trả về:
        /// - 200 OK: Trả về thành công dữ liệu giới hạn theo loại tài khoản.
        /// - 400 Bad Request: Nếu từ khóa rỗng hoặc khoảng năm sai.
        /// - 401 Unauthorized: Nếu chưa đăng nhập.
        /// </returns>
        [Authorize]
        [HttpGet("keyword/co-occurring")]
        public async Task<IActionResult> GetCoOccurringKeywordsAsync(
            [FromQuery] string keyword,
            [FromQuery] int fromYear = 2022,
            [FromQuery] int toYear = 0,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return BadRequest(ApiResponse<object>.Fail(400, "Keyword cannot be empty."));
            if (toYear <= 0) toYear = DateTime.UtcNow.Year;
            if (fromYear > toYear)
                return BadRequest(ApiResponse<object>.Fail(400, "fromYear cannot be greater than toYear."));

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(ApiResponse<object>.Fail(401, "Invalid identity."));
            }

            var subStatus = await _subscriptionService.GetSubscriptionStatusAsync(userId, ct);
            bool isPremium = subStatus.IsPremiumActive;
            int limit = isPremium ? 20 : 2;

            var keywords = await _trendService.GetCoOccurringKeywordsAsync(keyword.Trim(), fromYear, toYear, limit, ct);

            var message = isPremium 
                ? $"Premium Account: Found {keywords.Count} co-occurring keyword(s)." 
                : $"Free Account: Showing only 2 most common co-occurring keywords. Upgrade to Premium to see the full Top 20.";

            return Ok(ApiResponse<List<CoOccurringKeywordDto>>.Ok(keywords, message));
        }

        private static bool IsValidDimension(string d) =>
            !string.IsNullOrWhiteSpace(d) && ValidDimensions.Contains(d.Trim().ToLower());
    }
}

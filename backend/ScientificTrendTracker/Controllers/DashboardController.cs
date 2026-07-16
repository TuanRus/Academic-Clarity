using Microsoft.AspNetCore.Mvc;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs.Dashboard;
using ScientificTrendTracker.Services.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace ScientificTrendTracker.Controllers
{
    /// <summary>
    /// Controller cung cấp các API báo cáo thống kê và dữ liệu biểu đồ trên Dashboard dành cho Admin.
    /// </summary>
    [ApiController]
    [Route("api/admin/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        /// <summary>
        /// Lấy các chỉ số thống kê tổng quan (Dashboard Stats) hiển thị dạng thẻ.
        /// </summary>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime tự động cung cấp.</param>
        /// <returns>
        /// Trả về đối tượng ApiResponse bọc AdminDashboardStatsDto.
        /// - isSuccess (bool): true nếu lấy thành công.
        /// - statusCode (int): 200 OK.
        /// - data (Object): Chứa các thuộc tính TotalPapers, TotalAuthors, TotalRevenue, ActiveSubscriptions, NewPapersThisWeek.
        /// </returns>
        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStatsAsync(CancellationToken ct)
        {
            var result = await _dashboardService.GetDashboardStatsAsync(ct);
            return Ok(ApiResponse<AdminDashboardStatsDto>.Ok(result, "Successfully retrieved dashboard statistics."));
        }

        /// <summary>
        /// Lấy dữ liệu biểu đồ theo chuỗi thời gian phục vụ vẽ đồ thị (Monthly Revenue, Plan Distribution, Paper Growth).
        /// </summary>
        /// <param name="ct">CancellationToken - NGUỒN: ASP.NET Core runtime tự động cung cấp.</param>
        /// <returns>
        /// Trả về đối tượng ApiResponse bọc AdminDashboardChartsDto.
        /// - isSuccess (bool): true nếu lấy thành công.
        /// - statusCode (int): 200 OK.
        /// - data (Object): Chứa các danh sách MonthlyRevenues, PlanDistributions, PaperGrowths.
        /// </returns>
        [HttpGet("charts")]
        public async Task<IActionResult> GetDashboardChartsAsync(CancellationToken ct)
        {
            var result = await _dashboardService.GetDashboardChartsAsync(ct);
            return Ok(ApiResponse<AdminDashboardChartsDto>.Ok(result, "Successfully retrieved dashboard chart data."));
        }
    }
}

using System.Threading;
using System.Threading.Tasks;
using ScientificTrendTracker.Models.DTOs.Dashboard;

namespace ScientificTrendTracker.Services.Interfaces
{
    /// <summary>
    /// Giao diện dịch vụ cung cấp số liệu thống kê và dữ liệu biểu đồ cho Dashboard Admin.
    /// </summary>
    public interface IDashboardService
    {
        /// <summary>
        /// Lấy các chỉ số thống kê tổng quan (Dashboard Stats).
        /// </summary>
        /// <param name="ct">CancellationToken - Khởi tạo từ ASP.NET Core runtime.</param>
        /// <returns>Đối tượng AdminDashboardStatsDto chứa tổng số bài báo, doanh thu, gói đăng ký đang hoạt động,...</returns>
        Task<AdminDashboardStatsDto> GetDashboardStatsAsync(CancellationToken ct);

        /// <summary>
        /// Lấy dữ liệu vẽ các biểu đồ báo cáo trên Dashboard (Doanh thu tháng, phân bố gói cước, tốc độ bài báo).
        /// </summary>
        /// <param name="ct">CancellationToken - Khởi tạo từ ASP.NET Core runtime.</param>
        /// <returns>Đối tượng AdminDashboardChartsDto chứa danh sách chuỗi dữ liệu biểu đồ.</returns>
        Task<AdminDashboardChartsDto> GetDashboardChartsAsync(CancellationToken ct);
    }
}

using System.Collections.Generic;

namespace ScientificTrendTracker.Models.DTOs.Dashboard
{
    /// <summary>
    /// Đối tượng chứa các số liệu thống kê tổng quan hiển thị trên thẻ Dashboard của Admin.
    /// </summary>
    public class AdminDashboardStatsDto
    {
        /// <summary>
        /// Tổng số bài báo khoa học hiện có trong hệ thống.
        /// </summary>
        public int TotalPapers { get; set; }

        /// <summary>
        /// Tổng số tác giả hiện có trong hệ thống.
        /// </summary>
        public int TotalAuthors { get; set; }

        /// <summary>
        /// Tổng doanh thu tích lũy từ các gói đăng ký dịch vụ (VNĐ).
        /// </summary>
        public decimal TotalRevenue { get; set; }

        /// <summary>
        /// Tổng số gói đăng ký của người dùng đang có trạng thái hoạt động (ACTIVE).
        /// </summary>
        public int ActiveSubscriptions { get; set; }

        /// <summary>
        /// Số lượng bài báo mới được hệ thống nạp vào trong 7 ngày qua.
        /// </summary>
        public int NewPapersThisWeek { get; set; }

        /// <summary>
        /// Số NGƯỜI DÙNG (distinct) đang có ít nhất một gói Premium còn hiệu lực.
        /// Khác với ActiveSubscriptions (đếm theo dòng đăng ký, có thể trùng user do gia hạn/cộng dồn).
        /// </summary>
        public int PremiumUsers { get; set; }
    }

    /// <summary>
    /// Điểm dữ liệu cho biểu đồ doanh thu theo từng tháng.
    /// </summary>
    public class MonthlyRevenueDto
    {
        /// <summary>
        /// Nhãn tháng (định dạng MM/yyyy, ví dụ: 06/2026).
        /// </summary>
        public string Month { get; set; } = string.Empty;

        /// <summary>
        /// Tổng doanh thu của tháng đó (VNĐ).
        /// </summary>
        public decimal Revenue { get; set; }
    }

    /// <summary>
    /// Điểm dữ liệu cho biểu đồ phân bố gói cước dịch vụ được người dùng mua.
    /// </summary>
    public class PlanDistributionDto
    {
        /// <summary>
        /// Tên gói cước đăng ký (ví dụ: Premium, VIP, Basic).
        /// </summary>
        public string PlanName { get; set; } = string.Empty;

        /// <summary>
        /// Số lượng người dùng đã mua gói cước này.
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// Điểm dữ liệu cho biểu đồ tốc độ tăng trưởng số bài báo theo tháng.
    /// </summary>
    public class PaperGrowthDto
    {
        /// <summary>
        /// Nhãn tháng (định dạng MM/yyyy, ví dụ: 06/2026).
        /// </summary>
        public string Month { get; set; } = string.Empty;

        /// <summary>
        /// Số lượng bài báo được thêm mới trong tháng đó.
        /// </summary>
        public int NewPapersCount { get; set; }
    }

    /// <summary>
    /// Đối tượng tổng hợp toàn bộ dữ liệu vẽ các biểu đồ trên Dashboard của Admin.
    /// </summary>
    public class AdminDashboardChartsDto
    {
        /// <summary>
        /// Dữ liệu vẽ biểu đồ doanh thu theo tháng (Monthly Revenue).
        /// </summary>
        public List<MonthlyRevenueDto> MonthlyRevenues { get; set; } = new();

        /// <summary>
        /// Dữ liệu vẽ biểu đồ phân bố loại gói cước được mua (Plan Distribution).
        /// </summary>
        public List<PlanDistributionDto> PlanDistributions { get; set; } = new();

        /// <summary>
        /// Dữ liệu vẽ biểu đồ tốc độ tăng trưởng bài báo theo tháng (Paper Growth).
        /// </summary>
        public List<PaperGrowthDto> PaperGrowths { get; set; } = new();
    }
}

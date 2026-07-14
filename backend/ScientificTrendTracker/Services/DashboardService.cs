using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.DTOs.Dashboard;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Thực thi dịch vụ tính toán báo cáo Dashboard và biểu đồ cho quản trị hệ thống.
    /// </summary>
    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext _dbContext;

        public DashboardService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Lấy các chỉ số thống kê tổng quan (Dashboard Stats).
        /// </summary>
        public async Task<AdminDashboardStatsDto> GetDashboardStatsAsync(CancellationToken ct)
        {
            var totalPapers = await _dbContext.ResearchPapers.CountAsync(ct);
            var totalAuthors = await _dbContext.Authors.CountAsync(ct);

            // Tính tổng doanh thu từ việc đăng ký gói dịch vụ
            decimal totalRevenue = 0;
            var hasSubscriptions = await _dbContext.UserSubscriptions.AnyAsync(ct);
            if (hasSubscriptions)
            {
                totalRevenue = await _dbContext.UserSubscriptions
                    .Include(us => us.Plan)
                    .SumAsync(us => us.PaidAmount ?? (us.Plan != null ? us.Plan.PriceAmount : 0), ct);
            }

            // Đếm số gói cước đang kích hoạt của người dùng
            var activeSubscriptions = await _dbContext.UserSubscriptions
                .CountAsync(us => us.Status == "ACTIVE" && (us.EndsAt == null || us.EndsAt > DateTime.UtcNow), ct);

            // Đếm số NGƯỜI DÙNG distinct đang có Premium còn hiệu lực (không đếm trùng do gia hạn/cộng dồn).
            var premiumUsers = await _dbContext.UserSubscriptions
                .Where(us => us.Status == "ACTIVE" && (us.EndsAt == null || us.EndsAt > DateTime.UtcNow))
                .Select(us => us.UserId)
                .Distinct()
                .CountAsync(ct);

            // Đếm số bài viết được cập nhật mới trong 7 ngày qua
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            var newPapersThisWeek = await _dbContext.ResearchPapers
                .CountAsync(p => p.CreatedAt >= sevenDaysAgo, ct);

            return new AdminDashboardStatsDto
            {
                TotalPapers = totalPapers,
                TotalAuthors = totalAuthors,
                TotalRevenue = totalRevenue,
                ActiveSubscriptions = activeSubscriptions,
                NewPapersThisWeek = newPapersThisWeek,
                PremiumUsers = premiumUsers
            };
        }

        /// <summary>
        /// Lấy dữ liệu vẽ các biểu đồ báo cáo trên Dashboard.
        /// </summary>
        public async Task<AdminDashboardChartsDto> GetDashboardChartsAsync(CancellationToken ct)
        {
            var result = new AdminDashboardChartsDto();

            // 1. Biểu đồ doanh thu 12 tháng gần đây
            var twelveMonthsAgo = DateTime.UtcNow.AddMonths(-12);
            var rawRevenues = await _dbContext.UserSubscriptions
                .Include(us => us.Plan)
                .Where(us => us.CreatedAt >= twelveMonthsAgo)
                .Select(us => new { us.CreatedAt, Price = us.PaidAmount ?? (us.Plan != null ? us.Plan.PriceAmount : 0) })
                .ToListAsync(ct);

            result.MonthlyRevenues = rawRevenues
                .GroupBy(x => x.CreatedAt.ToString("MM/yyyy"))
                .Select(g => new MonthlyRevenueDto
                {
                    Month = g.Key,
                    Revenue = g.Sum(x => x.Price)
                })
                .OrderBy(g => DateTime.ParseExact(g.Month, "MM/yyyy", null))
                .ToList();

            // 2. Biểu đồ phân bố gói cước được mua
            result.PlanDistributions = await _dbContext.UserSubscriptions
                .Include(us => us.Plan)
                .GroupBy(us => us.Plan != null ? us.Plan.PlanName : "Unknown")
                .Select(g => new PlanDistributionDto
                {
                    PlanName = g.Key,
                    Count = g.Count()
                })
                .ToListAsync(ct);

            // 3. Biểu đồ tăng trưởng bài báo trong 6 tháng qua
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
            var rawPapers = await _dbContext.ResearchPapers
                .Where(p => p.CreatedAt >= sixMonthsAgo)
                .Select(p => new { p.CreatedAt })
                .ToListAsync(ct);

            result.PaperGrowths = rawPapers
                .GroupBy(x => x.CreatedAt.ToString("MM/yyyy"))
                .Select(g => new PaperGrowthDto
                {
                    Month = g.Key,
                    NewPapersCount = g.Count()
                })
                .OrderBy(g => DateTime.ParseExact(g.Month, "MM/yyyy", null))
                .ToList();

            return result;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Tính dữ liệu Trend Dashboard theo TẦN SUẤT TƯƠNG ĐỐI (share = bài chứa entity / tổng bài kỳ đó),
    /// nên không bị lệch khi mỗi kỳ crawl số lượng bài khác nhau. Hỗ trợ 3 chiều keyword/author/journal,
    /// gộp theo năm hoặc tháng.
    /// </summary>
    public class TrendService : ITrendService
    {
        private readonly AppDbContext _dbContext;
        private const double SlopeEpsilon = 0.0001;

        public TrendService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<TrendSeriesDto> GetSeriesAsync(string dimension, string value, int fromYear, int toYear, string groupBy)
        {
            var entityQuery = EntityPapers(dimension, value);
            if (entityQuery == null || !await entityQuery.AnyAsync())
                return null;

            var byMonth = string.Equals(groupBy, "month", StringComparison.OrdinalIgnoreCase);

            var entityCounts = await PeriodCountsAsync(entityQuery, fromYear, toYear, byMonth);
            var totals = await PeriodCountsAsync(_dbContext.ResearchPapers, fromYear, toYear, byMonth);

            var series = totals
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv =>
                {
                    var count = entityCounts.TryGetValue(kv.Key, out var c) ? c : 0;
                    return new TrendPointDto
                    {
                        Period = kv.Key,
                        Count = count,
                        PeriodTotal = kv.Value,
                        Share = kv.Value > 0 ? Math.Round((double)count / kv.Value, 6) : 0
                    };
                })
                .ToList();

            var slope = ComputeSlope(series);

            return new TrendSeriesDto
            {
                Dimension = dimension.ToLower(),
                Value = value,
                GroupBy = byMonth ? "month" : "year",
                TotalPapers = entityCounts.Values.Sum(),
                Series = series,
                Slope = Math.Round(slope, 6),
                Direction = Classify(slope)
            };
        }

        public async Task<List<TrendTopItemDto>> GetTopAsync(string dimension, int fromYear, int toYear, int topN, int minPapers, string sortBy)
        {
            var rangeTotal = await _dbContext.ResearchPapers
                .CountAsync(p => p.PublicationYear != null && p.PublicationYear >= fromYear && p.PublicationYear <= toYear);
            if (rangeTotal == 0) return new List<TrendTopItemDto>();

            // Tổng bài mỗi năm (để tính share theo năm → slope)
            var yearTotals = await _dbContext.ResearchPapers
                .Where(p => p.PublicationYear != null && p.PublicationYear >= fromYear && p.PublicationYear <= toYear)
                .GroupBy(p => p.PublicationYear.Value)
                .Select(g => new { Year = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.Year, x => x.Total);

            // Số bài theo (entity, năm)
            var rows = await GetEntityYearCountsAsync(dimension, fromYear, toYear);

            var items = rows
                .GroupBy(r => r.Name)
                .Select(g =>
                {
                    var total = g.Sum(x => x.Count);
                    // Chuỗi share theo năm (tăng dần) để tính slope
                    var series = yearTotals.Keys.OrderBy(y => y)
                        .Select(y =>
                        {
                            var cnt = g.FirstOrDefault(x => x.Year == y)?.Count ?? 0;
                            return new TrendPointDto { Period = y.ToString(), Share = yearTotals[y] > 0 ? (double)cnt / yearTotals[y] : 0 };
                        })
                        .ToList();
                    var slope = ComputeSlope(series);
                    return new TrendTopItemDto
                    {
                        Name = g.Key,
                        Count = total,
                        RangeTotal = rangeTotal,
                        Share = Math.Round((double)total / rangeTotal, 6),
                        Slope = Math.Round(slope, 6),
                        Direction = Classify(slope)
                    };
                })
                .Where(x => x.Count >= minPapers);

            // Sắp xếp theo tiêu chí
            items = (sortBy?.ToLower()) switch
            {
                "rising" => items.OrderByDescending(x => x.Slope),   // đang lên mạnh nhất
                "falling" => items.OrderBy(x => x.Slope),            // đang xuống mạnh nhất
                _ => items.OrderByDescending(x => x.Count)           // share/count (mặc định)
            };

            return items.Take(topN).ToList();
        }

        /// <summary>Đếm số bài theo (tên entity, năm) cho keyword/author/journal trong khoảng năm.</summary>
        private async Task<List<EntityYearCount>> GetEntityYearCountsAsync(string dimension, int fromYear, int toYear)
        {
            switch (dimension.ToLower())
            {
                case "author":
                    return await _dbContext.PaperAuthors
                        .Where(pa => pa.Paper.PublicationYear != null && pa.Paper.PublicationYear >= fromYear && pa.Paper.PublicationYear <= toYear)
                        .GroupBy(pa => new { pa.Author.FullName, Year = pa.Paper.PublicationYear.Value })
                        .Select(g => new EntityYearCount { Name = g.Key.FullName, Year = g.Key.Year, Count = g.Count() })
                        .ToListAsync();
                case "journal":
                    return await _dbContext.ResearchPapers
                        .Where(p => p.JournalId != null && p.PublicationYear != null && p.PublicationYear >= fromYear && p.PublicationYear <= toYear)
                        .GroupBy(p => new { p.Journal.JournalName, Year = p.PublicationYear.Value })
                        .Select(g => new EntityYearCount { Name = g.Key.JournalName, Year = g.Key.Year, Count = g.Count() })
                        .ToListAsync();
                default: // keyword
                    return await _dbContext.PaperKeywords
                        .Where(pk => pk.Paper.PublicationYear != null && pk.Paper.PublicationYear >= fromYear && pk.Paper.PublicationYear <= toYear)
                        .GroupBy(pk => new { pk.Keyword.KeywordName, Year = pk.Paper.PublicationYear.Value })
                        .Select(g => new EntityYearCount { Name = g.Key.KeywordName, Year = g.Key.Year, Count = g.Count() })
                        .ToListAsync();
            }
        }

        private class EntityYearCount { public string Name { get; set; } public int Year { get; set; } public int Count { get; set; } }

        /// <summary>Trả IQueryable các bài khớp entity theo chiều (keyword exact, author/journal contains). Null nếu chiều lạ.</summary>
        private IQueryable<ResearchPaper> EntityPapers(string dimension, string value)
        {
            var v = (value ?? string.Empty).Trim();
            return dimension.ToLower() switch
            {
                "keyword" => _dbContext.ResearchPapers
                    .Where(p => p.PaperKeywords.Any(pk => pk.Keyword.KeywordName == v.ToLower())),
                "author" => _dbContext.ResearchPapers
                    .Where(p => p.PaperAuthors.Any(pa => pa.Author.FullName.Contains(v))),
                "journal" => _dbContext.ResearchPapers
                    .Where(p => p.Journal != null && p.Journal.JournalName.Contains(v)),
                _ => null
            };
        }

        /// <summary>
        /// Đếm số bài theo kỳ (năm hoặc tháng) cho 1 tập bài. Năm dùng PublicationYear; tháng dùng PublicationDate
        /// (bài thiếu PublicationDate bị bỏ khi gộp theo tháng).
        /// </summary>
        private static async Task<Dictionary<string, int>> PeriodCountsAsync(
            IQueryable<ResearchPaper> query, int fromYear, int toYear, bool byMonth)
        {
            if (byMonth)
            {
                var rows = await query
                    .Where(p => p.PublicationDate != null
                             && p.PublicationDate.Value.Year >= fromYear && p.PublicationDate.Value.Year <= toYear)
                    .GroupBy(p => new { p.PublicationDate.Value.Year, p.PublicationDate.Value.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                    .ToListAsync();
                return rows.ToDictionary(r => $"{r.Year:D4}-{r.Month:D2}", r => r.Count);
            }

            var years = await query
                .Where(p => p.PublicationYear != null && p.PublicationYear >= fromYear && p.PublicationYear <= toYear)
                .GroupBy(p => p.PublicationYear.Value)
                .Select(g => new { Year = g.Key, Count = g.Count() })
                .ToListAsync();
            return years.ToDictionary(r => r.Year.ToString(), r => r.Count);
        }

        /// <summary>Độ dốc hồi quy tuyến tính của Share theo thứ tự kỳ (least squares).</summary>
        private static double ComputeSlope(List<TrendPointDto> series)
        {
            if (series.Count < 2) return 0;
            double xbar = (series.Count - 1) / 2.0; // index 0..n-1
            double ybar = series.Average(p => p.Share);
            double num = 0, den = 0;
            for (int i = 0; i < series.Count; i++)
            {
                num += (i - xbar) * (series[i].Share - ybar);
                den += (i - xbar) * (i - xbar);
            }
            return den == 0 ? 0 : num / den;
        }

        private static string Classify(double slope) =>
            slope > SlopeEpsilon ? "rising" : slope < -SlopeEpsilon ? "falling" : "stable";
    }
}

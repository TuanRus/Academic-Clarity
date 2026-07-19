using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    public class GraphBuilderService : IGraphBuilderService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<GraphBuilderService> _logger;
        private readonly IOpenAlexService _openAlexService;

        // Bài báo từ năm này trở đi được tính là "gần đây" để tính TrendScore
        private const int RecentYearThreshold = 2023;

        // Keyword phụ chỉ hiện nếu xuất hiện ở >= ngần này bài — lọc keyword "một lần" gây nhiễu graph
        private const int MinKeywordPaperCount = 2;

        public GraphBuilderService(AppDbContext dbContext, ILogger<GraphBuilderService> logger, IOpenAlexService openAlexService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _openAlexService = openAlexService;
        }

        /// <summary>
        /// Chi tiết đầy đủ 1 bài báo cho màn Paper Detail của FE.
        /// Thông tin cốt lõi từ DB; Abstract reconstruct on-demand từ OpenAlex (func có sẵn, không lưu DB).
        /// </summary>
        public async Task<PaperDetailDto> GetPaperDetailAsync(string paperId)
        {
            var paper = await _dbContext.ResearchPapers
                .Include(p => p.Journal)
                .Include(p => p.PaperKeywords)
                    .ThenInclude(pk => pk.Keyword)
                .Include(p => p.PaperAuthors)
                    .ThenInclude(pa => pa.Author)
                .FirstOrDefaultAsync(p => p.PaperId == paperId);

            if (paper == null)
            {
                _logger.LogInformation("Không tìm thấy paper detail với PaperId '{PaperId}'.", paperId);
                return null;
            }

            // OpenAlex: 1 request lấy abstract + topic/subfield/field/domain + OA status + institutions.
            // (Topic đã lưu DB lúc sync; subfield/field/domain/OA/institutions lấy on-demand.)
            OpenAlexWorkDetail oaDetail = null;
            if (!string.IsNullOrWhiteSpace(paper.OpenAlexId))
            {
                try
                {
                    oaDetail = await _openAlexService.FetchWorkDetailAsync(paper.OpenAlexId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không lấy được chi tiết OpenAlex cho '{OpenAlexId}'.", paper.OpenAlexId);
                }
            }

            return new PaperDetailDto
            {
                PaperId = paper.PaperId,
                OpenAlexId = paper.OpenAlexId,
                Doi = paper.Doi,
                Title = paper.Title,
                PublicationYear = paper.PublicationYear,
                PublicationDate = paper.PublicationDate?.ToString("yyyy-MM-dd"),
                CitationCount = paper.CitationCount,
                SourceUrl = paper.SourceUrl,
                JournalName = paper.Journal?.JournalName,
                Quartile = paper.Journal?.QuartileRank,
                Publisher = paper.Journal?.Publisher,
                ImpactFactor = paper.Journal?.ImpactFactor,
                Authors = paper.PaperAuthors
                    .OrderBy(pa => pa.AuthorOrder)
                    .Select(pa => pa.Author.FullName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList(),
                Keywords = paper.PaperKeywords
                    .Select(pk => pk.Keyword.KeywordName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList(),
                // Topic ưu tiên DB; fallback OpenAlex live cho bài cũ chưa sync lại cột Topic.
                Topic = !string.IsNullOrWhiteSpace(paper.Topic) ? paper.Topic : oaDetail?.Topic,
                Subfield = oaDetail?.Subfield,
                Field = oaDetail?.Field,
                Domain = oaDetail?.Domain,
                OpenAccessStatus = oaDetail?.OpenAccessStatus,
                Institutions = oaDetail?.Institutions ?? new List<string>(),
                Abstract = oaDetail?.Abstract,
            };
        }

        /// <summary>
        /// Dựng mind map dạng CÂY 3 tầng KEYWORD: tầng 0 = chủ đề trung tâm, tầng 1 = chủ đề con
        /// (keyword đồng xuất hiện với gốc), tầng 2 = chủ đề cháu (keyword đồng xuất hiện với từng chủ đề con).
        /// KHÔNG gắn bài báo lên cây — FE gọi /papers/keyword khi user click 1 node để xổ danh sách bài.
        /// </summary>
        /// <param name="keyword">
        /// string - Controller truyền vào (từ query FE) - Chủ đề trung tâm, ưu tiên khớp chính xác rồi contains.
        /// </param>
        /// <param name="maxBranches">
        /// int - Controller truyền vào - Số chủ đề con (tầng 1), mặc định 6.
        /// </param>
        /// <param name="maxSubBranches">
        /// int - Controller truyền vào - Số chủ đề cháu mỗi nhánh (tầng 2), mặc định 3.
        /// </param>
        /// <returns>
        /// MindmapGraphDto - Object cho FE render cây. Gồm SearchQuery, TotalNodes, TotalEdges,
        /// Nodes (toàn node keyword, Level 0/1/2) và Edges (cha→con). Rỗng nếu không tìm thấy keyword.
        /// </returns>
        public async Task<MindmapGraphDto> BuildKeywordTreeAsync(string keyword, int maxBranches = 6, int maxSubBranches = 3)
        {
            var graph = new MindmapGraphDto { SearchQuery = keyword };
            var lowered = keyword.Trim().ToLower();

            // 1. Tìm keyword trung tâm (ưu tiên exact, fallback contains theo paperCount cao nhất)
            var central = await _dbContext.Keywords.FirstOrDefaultAsync(k => k.KeywordName == lowered)
                ?? await _dbContext.Keywords
                    .Where(k => k.KeywordName.Contains(lowered))
                    .OrderByDescending(k => k.PaperKeywords.Count)
                    .FirstOrDefaultAsync();

            if (central == null)
            {
                _logger.LogInformation("Tree: không tìm thấy keyword '{Keyword}'.", keyword);
                return graph;
            }

            // Theo dõi keyword đã thêm để tránh trùng node giữa các tầng/nhánh
            var addedKeywordIds = new HashSet<string> { central.KeywordId };
            var centralNodeId = $"kw_{central.KeywordId}";
            graph.Nodes.Add(await BuildKeywordNodeAsync(central.KeywordId, central.KeywordName, level: 0));

            // 2. Tầng 1 — chủ đề con: keyword đồng xuất hiện với gốc
            var subKeywords = await FindCoKeywordsAsync(central.KeywordId, addedKeywordIds, maxBranches);
            // Thêm hết id tầng 1 vào tập đã dùng TRƯỚC khi tính tầng 2 (để cháu không trùng con)
            foreach (var sub in subKeywords) addedKeywordIds.Add(sub.KeywordId);

            foreach (var sub in subKeywords)
            {
                var subNodeId = $"kw_{sub.KeywordId}";
                graph.Nodes.Add(await BuildKeywordNodeAsync(sub.KeywordId, sub.KeywordName, level: 1));
                graph.Edges.Add(new MindmapEdgeDto { Source = centralNodeId, Target = subNodeId });

                // 3. Tầng 2 — chủ đề cháu: keyword đồng xuất hiện với chủ đề con này (loại trùng)
                var subSub = await FindCoKeywordsAsync(sub.KeywordId, addedKeywordIds, maxSubBranches);
                foreach (var ss in subSub)
                {
                    addedKeywordIds.Add(ss.KeywordId);
                    var ssNodeId = $"kw_{ss.KeywordId}";
                    graph.Nodes.Add(await BuildKeywordNodeAsync(ss.KeywordId, ss.KeywordName, level: 2));
                    graph.Edges.Add(new MindmapEdgeDto { Source = subNodeId, Target = ssNodeId });
                }
            }

            return Finalize(graph, keyword);
        }

        /// <summary>
        /// Tìm các keyword đồng xuất hiện với keyword cho trước (trên cùng bài), xếp theo số lần đồng xuất hiện.
        /// Loại các keyword đã có trong excludeIds (tránh trùng node trên cây).
        /// </summary>
        /// <param name="keywordId">string - Caller truyền vào - Keyword gốc để tìm đồng xuất hiện.</param>
        /// <param name="excludeIds">HashSet&lt;string&gt; - Caller truyền vào - Các keyword đã thêm, cần loại.</param>
        /// <param name="take">int - Caller truyền vào - Số keyword tối đa trả về.</param>
        /// <returns>List - Mỗi phần tử gồm KeywordId, KeywordName (đã loại trùng, đồng xuất hiện &gt;= 2 lần).</returns>
        private async Task<List<CoKeyword>> FindCoKeywordsAsync(string keywordId, HashSet<string> excludeIds, int take)
        {
            var paperIds = await _dbContext.PaperKeywords
                .Where(pk => pk.KeywordId == keywordId)
                .Select(pk => pk.PaperId)
                .ToListAsync();

            if (paperIds.Count == 0) return new List<CoKeyword>();

            return await _dbContext.PaperKeywords
                .Where(pk => paperIds.Contains(pk.PaperId) && !excludeIds.Contains(pk.KeywordId))
                .GroupBy(pk => new { pk.KeywordId, pk.Keyword.KeywordName })
                .Select(g => new CoKeyword { KeywordId = g.Key.KeywordId, KeywordName = g.Key.KeywordName, CoCount = g.Count() })
                .Where(x => x.CoCount >= 2)
                .OrderByDescending(x => x.CoCount)
                .Take(take)
                .ToListAsync();
        }

        /// <summary>Tạo 1 node keyword kèm PaperCount + TrendScore (tỷ lệ bài gần đây / tổng bài).</summary>
        /// <param name="keywordId">string - Caller truyền vào - Id keyword.</param>
        /// <param name="label">string - Caller truyền vào - Tên keyword hiển thị.</param>
        /// <param name="level">int - Caller truyền vào - Tầng trên cây (0/1/2).</param>
        /// <returns>MindmapNodeDto - Node keyword đã điền PaperCount, TrendScore, Level.</returns>
        private async Task<MindmapNodeDto> BuildKeywordNodeAsync(string keywordId, string label, int level)
        {
            var total = await _dbContext.PaperKeywords.CountAsync(x => x.KeywordId == keywordId);
            var recent = await _dbContext.PaperKeywords
                .CountAsync(x => x.KeywordId == keywordId && x.Paper.PublicationYear >= RecentYearThreshold);
            return new MindmapNodeDto
            {
                Id = $"kw_{keywordId}", Type = "keyword", Label = label,
                PaperCount = total, Level = level,
                TrendScore = total > 0 ? Math.Round((double)recent / total, 2) : 0
            };
        }

        /// <summary>Keyword đồng xuất hiện kèm số lần — dùng nội bộ khi dựng cây.</summary>
        private class CoKeyword
        {
            public string KeywordId { get; set; }
            public string KeywordName { get; set; }
            public int CoCount { get; set; }
        }

        private MindmapGraphDto Finalize(MindmapGraphDto graph, string keyword)
        {
            graph.TotalNodes = graph.Nodes.Count;
            graph.TotalEdges = graph.Edges.Count;
            _logger.LogInformation("Tree '{Keyword}': {Nodes} nodes, {Edges} edges.", keyword, graph.TotalNodes, graph.TotalEdges);
            return graph;
        }

        /// <summary>
        /// Tìm bài báo theo TIÊU ĐỀ hoặc DOI (contains) → danh sách phân trang cho FE chọn.
        /// </summary>
        /// <param name="query">
        /// string - Controller truyền vào (từ query FE ?q=) - Từ khóa tìm trong Title hoặc Doi.
        /// </param>
        /// <param name="page">int - Controller truyền vào - Trang hiện tại, bắt đầu từ 1.</param>
        /// <param name="pageSize">int - Controller truyền vào - Số bài mỗi trang.</param>
        /// <returns>
        /// PagedResult&lt;PaperSearchItemDto&gt; - Items (mỗi bài: PaperId, Title, Year, CitationCount,
        /// JournalName, Quartile, SourceUrl, KeywordCount) + TotalCount, Page, PageSize. Sắp theo citation giảm dần.
        /// </returns>
        public async Task<PagedResult<PaperSearchItemDto>> SearchPapersAsync(
            string query, int page, int pageSize, int? fromYear = null, int? toYear = null)
        {
            var q = (query ?? string.Empty).Trim();
            // Keyword luu dang co gach noi (vd "large-language-models") -> chuan hoa de khop ca khi go dau cach.
            var kw = KeywordNormalizer.Normalize(q);

            var baseQuery = _dbContext.ResearchPapers
                .Where(p => p.Title.Contains(q)
                         || (p.Doi != null && p.Doi.Contains(q))
                         || p.PaperKeywords.Any(pk => pk.Keyword.KeywordName.Contains(kw)));

            // Bộ lọc khoảng năm (Advanced Search) — chỉ áp dụng khi FE truyền lên.
            if (fromYear.HasValue)
                baseQuery = baseQuery.Where(p => p.PublicationYear >= fromYear.Value);
            if (toYear.HasValue)
                baseQuery = baseQuery.Where(p => p.PublicationYear <= toYear.Value);

            return await ToPagedPapersAsync(baseQuery, page, pageSize);
        }

        /// <summary>
        /// Tìm bài báo theo TÊN TÁC GIẢ (contains qua bảng PaperAuthors) → danh sách phân trang.
        /// </summary>
        /// <param name="author">
        /// string - Controller truyền vào (từ query FE ?author=) - Tên tác giả, tìm gần đúng.
        /// </param>
        /// <param name="page">int - Controller truyền vào - Trang hiện tại.</param>
        /// <param name="pageSize">int - Controller truyền vào - Số bài mỗi trang.</param>
        /// <returns>
        /// PagedResult&lt;PaperSearchItemDto&gt; - Cùng cấu trúc với SearchPapersAsync, sắp theo citation giảm dần.
        /// </returns>
        public async Task<PagedResult<PaperSearchItemDto>> SearchPapersByAuthorAsync(string author, int page, int pageSize)
        {
            var q = (author ?? string.Empty).Trim();
            var baseQuery = _dbContext.ResearchPapers
                .Where(p => p.PaperAuthors.Any(pa => pa.Author.FullName.Contains(q)));

            return await ToPagedPapersAsync(baseQuery, page, pageSize);
        }

        /// <summary>
        /// Tìm bài báo theo TÊN TẠP CHÍ (contains qua navigation Journal) → danh sách phân trang.
        /// </summary>
        /// <param name="journal">
        /// string - Controller truyền vào (từ query FE ?journal=) - Tên tạp chí, tìm gần đúng.
        /// </param>
        /// <param name="page">int - Controller truyền vào - Trang hiện tại.</param>
        /// <param name="pageSize">int - Controller truyền vào - Số bài mỗi trang.</param>
        /// <returns>
        /// PagedResult&lt;PaperSearchItemDto&gt; - Cùng cấu trúc với SearchPapersAsync, sắp theo citation giảm dần.
        /// </returns>
        public async Task<PagedResult<PaperSearchItemDto>> SearchPapersByJournalAsync(string journal, int page, int pageSize)
        {
            var q = (journal ?? string.Empty).Trim();
            var baseQuery = _dbContext.ResearchPapers
                .Where(p => p.Journal != null && p.Journal.JournalName.Contains(q));

            return await ToPagedPapersAsync(baseQuery, page, pageSize);
        }

        /// <summary>Phân trang + map sang PaperSearchItemDto, sắp theo citation giảm dần. Dùng chung cho mọi kiểu search.</summary>
        private async Task<PagedResult<PaperSearchItemDto>> ToPagedPapersAsync(
            IQueryable<Models.Entities.ResearchPaper> baseQuery, int page, int pageSize)
        {
            var totalCount = await baseQuery.CountAsync();

            var items = await baseQuery
                .OrderByDescending(p => p.CitationCount)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PaperSearchItemDto
                {
                    PaperId = p.PaperId,
                    Title = p.Title,
                    Year = p.PublicationYear,
                    CitationCount = p.CitationCount,
                    JournalName = p.Journal != null ? p.Journal.JournalName : null,
                    Quartile = p.Journal != null ? p.Journal.QuartileRank : null,
                    SourceUrl = p.SourceUrl,
                    KeywordCount = p.PaperKeywords.Count
                })
                .ToListAsync();

            return new PagedResult<PaperSearchItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// Lấy top bài báo của 1 keyword cho panel khi user click node. Nếu truyền distinctFrom (keyword root),
        /// bài KHÔNG chứa root được ưu tiên lên trước để sub-node hiện bài đặc trưng, tránh lặp megahit của root.
        /// </summary>
        /// <param name="keyword">
        /// string - Controller truyền vào (từ query FE) - Keyword của node được click (exact trước, fallback contains).
        /// </param>
        /// <param name="distinctFrom">
        /// string - Controller truyền vào (tùy chọn) - Keyword root đang xem; null khi click chính node root.
        /// </param>
        /// <param name="limit">int - Controller truyền vào - Số bài tối đa, mặc định 10.</param>
        /// <returns>
        /// List&lt;PaperSearchItemDto&gt; - Mỗi bài gồm PaperId, Title, Year, CitationCount, JournalName,
        /// Quartile, SourceUrl, KeywordCount. Rỗng nếu keyword không tồn tại.
        /// </returns>
        public async Task<List<PaperSearchItemDto>> GetTopPapersByKeywordAsync(
            string keyword, string distinctFrom = null, int limit = 10)
        {
            var kw = await FindKeywordAsync(keyword);
            if (kw == null) return new List<PaperSearchItemDto>();

            // Root đang xem (nếu có) — để ưu tiên bài đặc trưng cho sub-node (Cách A: deprioritize overlap)
            string rootId = null;
            if (!string.IsNullOrWhiteSpace(distinctFrom) && distinctFrom.Trim().ToLower() != kw.KeywordName)
                rootId = (await FindKeywordAsync(distinctFrom))?.KeywordId;

            var papersQuery = _dbContext.PaperKeywords
                .Where(pk => pk.KeywordId == kw.KeywordId)
                .Select(pk => pk.Paper);

            IOrderedQueryable<Models.Entities.ResearchPaper> ordered = rootId != null
                // Bài KHÔNG chứa root lên trước (giá trị 0), rồi citation desc → sub khác root
                ? papersQuery
                    .OrderBy(p => p.PaperKeywords.Any(x => x.KeywordId == rootId) ? 1 : 0)
                    .ThenByDescending(p => p.CitationCount)
                : papersQuery.OrderByDescending(p => p.CitationCount);

            return await ordered
                .Take(limit)
                .Select(p => new PaperSearchItemDto
                {
                    PaperId = p.PaperId,
                    Title = p.Title,
                    Year = p.PublicationYear,
                    CitationCount = p.CitationCount,
                    JournalName = p.Journal != null ? p.Journal.JournalName : null,
                    Quartile = p.Journal != null ? p.Journal.QuartileRank : null,
                    SourceUrl = p.SourceUrl,
                    KeywordCount = p.PaperKeywords.Count
                })
                .ToListAsync();
        }

        /// <summary>
        /// Gợi ý keyword có sẵn trong DB khớp chuỗi gõ vào, ưu tiên keyword nhiều bài nhất (autocomplete).
        /// </summary>
        /// <param name="q">string - Controller truyền vào - Chuỗi đang gõ (contains, lowercase).</param>
        /// <param name="limit">int - Controller truyền vào - Số gợi ý tối đa.</param>
        /// <returns>List&lt;string&gt; - Tên keyword khớp, sắp theo số bài giảm dần.</returns>
        public async Task<List<string>> SuggestKeywordsAsync(string q, int limit)
        {
            // Keyword luu dang co gach noi (vd "big-data") -> user go "big d"/"big data" cung khop.
            var lowered = KeywordNormalizer.Normalize(q);
            if (lowered.Length == 0) return new List<string>();

            return await _dbContext.Keywords
                .Where(k => k.KeywordName.Contains(lowered))
                .OrderByDescending(k => k.PaperKeywords.Count)
                .Take(limit)
                .Select(k => k.KeywordName)
                .ToListAsync();
        }

        /// <summary>Autocomplete tên tác giả (distinct) khớp prefix, sắp theo số bài giảm dần.</summary>
        public async Task<List<string>> SuggestAuthorsAsync(string q, int limit)
        {
            var term = (q ?? string.Empty).Trim();
            if (term.Length == 0) return new List<string>();

            return await _dbContext.Authors
                .Where(a => a.FullName.Contains(term))
                .OrderByDescending(a => a.PaperAuthors.Count)
                .ThenBy(a => a.FullName)
                .Take(limit)
                .Select(a => a.FullName)
                .ToListAsync();
        }

        /// <summary>Tìm keyword: ưu tiên exact, fallback contains theo paperCount cao nhất.</summary>
        private async Task<Models.Entities.Keyword> FindKeywordAsync(string keyword)
        {
            var lowered = KeywordNormalizer.Normalize(keyword);
            return await _dbContext.Keywords.FirstOrDefaultAsync(k => k.KeywordName == lowered)
                ?? await _dbContext.Keywords
                    .Where(k => k.KeywordName.Contains(lowered))
                    .OrderByDescending(k => k.PaperKeywords.Count)
                    .FirstOrDefaultAsync();
        }

    }
}

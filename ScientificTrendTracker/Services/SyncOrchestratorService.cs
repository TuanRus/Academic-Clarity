using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    public class SyncOrchestratorService : ISyncOrchestratorService
    {
        private const int DelayBetweenPagesMs = 500;
        private const int DelayBetweenPapersMs = 4000; // ~15 req/phút, dưới Gemini flash RPM (15)

        private readonly IOpenAlexService _openAlexService;
        private readonly IKeywordExtractionService _keywordService;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<SyncOrchestratorService> _logger;

        public SyncOrchestratorService(
            IOpenAlexService openAlexService,
            IKeywordExtractionService keywordService,
            AppDbContext dbContext,
            ILogger<SyncOrchestratorService> logger)
        {
            _openAlexService = openAlexService;
            _keywordService = keywordService;
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Fetch bài từ OpenAlex theo trang (maxPages) → lưu DB, bỏ qua bài đã có (dedup theo OpenAlexId).
        /// skipKeywords=true: chỉ lưu paper (IsAiProcessed=false); false: gọi AI trích keyword inline.
        /// Xem doc tham số đầy đủ ở ISyncOrchestratorService.
        /// </summary>
        /// <returns>
        /// SyncResult - Breakdown: Added (thêm mới), AlreadyExists (đã có), NoTitle (thiếu tiêu đề), Errors (lỗi).
        /// </returns>
        public async Task<SyncResult> RunSyncAsync(int maxPages, bool skipKeywords = false,
            int fromYear = 2022, int minCitedExclusive = 2, bool recentFirst = false,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("=== Bắt đầu Sync lúc {Time}, maxPages={MaxPages}, skipKeywords={Skip}, fromYear={FromYear}, recentFirst={Recent} ===",
                DateTime.UtcNow, maxPages, skipKeywords, fromYear, recentFirst);
            var result = new SyncResult();

            for (int page = 1; page <= maxPages; page++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                List<OpenAlexPaper> papers;
                try
                {
                    papers = await _openAlexService.FetchPapersAsync(
                        page, fromYear: fromYear, minCitedExclusive: minCitedExclusive, recentFirst: recentFirst);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi fetch OpenAlex page {Page}, dừng sync.", page);
                    break;
                }

                if (papers.Count == 0)
                {
                    _logger.LogInformation("OpenAlex hết data tại page {Page}, kết thúc sync.", page);
                    break;
                }

                var pausedByQuota = false;

                foreach (var paper in papers)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var throttle = false; // delay khi vừa gọi AI HOẶC vừa lỗi (để không spin)
                    try
                    {
                        var outcome = await ProcessPaperAsync(paper, skipKeywords);
                        switch (outcome)
                        {
                            case ProcessOutcome.Added:         result.Added++;         break;
                            case ProcessOutcome.AlreadyExists: result.AlreadyExists++; break;
                            case ProcessOutcome.NoTitle:       result.NoTitle++;       break;
                        }

                        // Chỉ throttle khi vừa gọi AI (Added + không skip). Fetch-only thì không delay → nạp nhanh.
                        if (outcome == ProcessOutcome.Added && !skipKeywords)
                            throttle = true;
                    }
                    catch (AllProvidersExhaustedException)
                    {
                        // Hết quota AI → dừng sync, paper chưa lưu sẽ được fetch lại lần sync sau
                        _logger.LogWarning("Sync DỪNG: mọi AI provider hết quota.");
                        _dbContext.ChangeTracker.Clear();
                        pausedByQuota = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi xử lý paper '{Title}': {Message}", paper.Title, ex.Message);
                        result.Errors++;
                        // Reset EF change tracker để paper tiếp theo không bị ảnh hưởng bởi state lỗi
                        _dbContext.ChangeTracker.Clear();
                        throttle = true; // lỗi (có thể do AI/mạng) → vẫn back off, tránh spin bắn liên tục
                    }
                    finally
                    {
                        // ĐẶT Ở FINALLY: delay luôn chạy dù try ném exception → không bao giờ spin
                        if (throttle)
                            await Task.Delay(DelayBetweenPapersMs, cancellationToken);
                    }
                }

                _logger.LogInformation(
                    "Page {Page}: added={Added} exists={Exists} noTitle={NoTitle} errors={Errors}",
                    page, result.Added, result.AlreadyExists, result.NoTitle, result.Errors);

                if (pausedByQuota) break;

                if (page < maxPages)
                    await Task.Delay(DelayBetweenPagesMs, cancellationToken);
            }

            _logger.LogInformation(
                "=== Sync hoàn thành: added={Added}, exists={Exists}, noTitle={NoTitle}, errors={Errors} ===",
                result.Added, result.AlreadyExists, result.NoTitle, result.Errors);

            return result;
        }

        /// <summary>
        /// Backfill CÂN BẰNG: lặp từng năm fromYear→toYear, mỗi năm lấy tối đa perYearCap bài cùng tiêu chí
        /// (năm ≤2024 sort citation, ≥2025 sort ngày + bỏ lọc citation) để mẫu mỗi năm đủ và fair.
        /// Xem doc tham số đầy đủ ở ISyncOrchestratorService.
        /// </summary>
        /// <returns>SyncResult - Added, AlreadyExists, NoTitle, Errors cộng dồn qua các năm.</returns>
        public async Task<SyncResult> RunBalancedBackfillAsync(int perYearCap = 2500, int fromYear = 2022,
            int toYear = 2026, bool skipKeywords = true, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("=== Backfill cân bằng: {From}-{To}, {Cap} bài/năm, skipKeywords={Skip} ===",
                fromYear, toYear, perYearCap, skipKeywords);
            var result = new SyncResult();

            const int pageSize = 200;
            // Trần paging OpenAlex: page*per-page ≤ 10,000 → tối đa 50 trang/năm.
            var maxPagesPerYear = Math.Min((int)Math.Ceiling(perYearCap / (double)pageSize), 50);

            for (int year = fromYear; year <= toYear; year++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // Năm gần (≥2025): sort theo ngày + bỏ lọc citation (bài mới chưa kịp được trích dẫn).
                var recentFirst = year >= 2025;
                var minCited = recentFirst ? -1 : 2;
                var fetchedThisYear = 0;

                for (int page = 1; page <= maxPagesPerYear && fetchedThisYear < perYearCap; page++)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    List<OpenAlexPaper> papers;
                    try
                    {
                        papers = await _openAlexService.FetchPapersAsync(
                            page, pageSize, fromYear: year, minCitedExclusive: minCited,
                            recentFirst: recentFirst, toYear: year);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Backfill lỗi fetch năm {Year} page {Page}, sang năm khác.", year, page);
                        break;
                    }

                    if (papers.Count == 0) break; // hết bài của năm này

                    foreach (var paper in papers)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        if (fetchedThisYear >= perYearCap) break;
                        fetchedThisYear++;

                        try
                        {
                            var outcome = await ProcessPaperAsync(paper, skipKeywords);
                            switch (outcome)
                            {
                                case ProcessOutcome.Added:         result.Added++;         break;
                                case ProcessOutcome.AlreadyExists: result.AlreadyExists++; break;
                                case ProcessOutcome.NoTitle:       result.NoTitle++;       break;
                            }
                        }
                        catch (AllProvidersExhaustedException)
                        {
                            // Chỉ xảy ra khi skipKeywords=false. Dừng hẳn, phần còn lại để reprocess-all xử lý.
                            _logger.LogWarning("Backfill DỪNG: mọi AI provider hết quota.");
                            _dbContext.ChangeTracker.Clear();
                            return result;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Backfill lỗi paper '{Title}': {Message}", paper.Title, ex.Message);
                            result.Errors++;
                            _dbContext.ChangeTracker.Clear();
                        }
                    }

                    await Task.Delay(DelayBetweenPagesMs, cancellationToken);
                }

                _logger.LogInformation("Backfill năm {Year}: fetched={Fetched} (added tổng={Added}, exists={Exists})",
                    year, fetchedThisYear, result.Added, result.AlreadyExists);
            }

            _logger.LogInformation("=== Backfill xong: added={Added}, exists={Exists}, noTitle={NoTitle}, errors={Errors} ===",
                result.Added, result.AlreadyExists, result.NoTitle, result.Errors);
            return result;
        }

        private enum ProcessOutcome { Added, AlreadyExists, NoTitle }

        /// <summary>
        /// Xử lý 1 bài từ OpenAlex: bỏ qua nếu thiếu title hoặc đã tồn tại; nếu chưa có thì tạo/link Journal,
        /// (tùy chọn) gọi AI trích keyword, rồi lưu ResearchPaper + Authors + Keywords vào DB.
        /// </summary>
        /// <param name="paper">
        /// OpenAlexPaper - IOpenAlexService.FetchPapersAsync trả về - Bài báo thô đã map từ JSON OpenAlex.
        /// </param>
        /// <param name="skipKeywords">
        /// bool - Caller truyền vào - true = không gọi AI (IsAiProcessed=false); false = trích keyword inline.
        /// </param>
        /// <returns>
        /// ProcessOutcome (enum) - Added (thêm mới) / AlreadyExists (đã có) / NoTitle (bỏ vì thiếu tiêu đề).
        /// </returns>
        private async Task<ProcessOutcome> ProcessPaperAsync(OpenAlexPaper paper, bool skipKeywords)
        {
            if (string.IsNullOrWhiteSpace(paper.Title))
                return ProcessOutcome.NoTitle;

            var openAlexId = paper.Id?.Replace("https://openalex.org/", "");
            var exists = await _dbContext.ResearchPapers.AnyAsync(p => p.OpenAlexId == openAlexId);
            if (exists)
                return ProcessOutcome.AlreadyExists;

            string journalId = null;
            var source = paper.PrimaryLocation?.Source;
            if (source != null && !string.IsNullOrWhiteSpace(source.IssnL))
            {
                var journal = await _dbContext.Journals
                    .FirstOrDefaultAsync(j => j.IssnPrint == source.IssnL || j.IssnElectronic == source.IssnL);

                if (journal == null)
                {
                    journal = new Journal
                    {
                        JournalId = Guid.NewGuid().ToString("N")[..20],
                        OpenAlexId = source.Id,
                        JournalName = source.DisplayName ?? "Unknown",
                        IssnPrint = source.IssnL,
                        IssnElectronic = source.Issn?.FirstOrDefault(i => i != source.IssnL)
                    };
                    _dbContext.Journals.Add(journal);
                    await _dbContext.SaveChangesAsync();
                }

                journalId = journal.JournalId;
            }

            // Fetch-only: bỏ qua AI, lưu paper với IsAiProcessed=false để reprocess-all xử lý keyword sau
            var keywords = new List<string>();
            if (!skipKeywords && !string.IsNullOrWhiteSpace(paper.AbstractReconstructed))
                keywords = await _keywordService.ExtractKeywordsAsync(paper.AbstractReconstructed, paper.Title);

            var newPaper = new ResearchPaper
            {
                PaperId = Guid.NewGuid().ToString("N")[..20],
                OpenAlexId = openAlexId,
                Doi = paper.Doi?.Replace("https://doi.org/", ""),
                Title = paper.Title[..Math.Min(500, paper.Title.Length)],
                PublicationYear = paper.PublicationYear,
                PublicationDate = DateTime.TryParse(paper.PublicationDate, out var dt) ? dt : null,
                CitationCount = paper.CitedByCount,
                JournalId = journalId,
                SourceUrl = paper.Id,
                IsAiProcessed = keywords.Count > 0,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ResearchPapers.Add(newPaper);
            await _dbContext.SaveChangesAsync();

            await SaveAuthorsAsync(paper, newPaper.PaperId);
            await SaveKeywordsAsync(keywords, newPaper.PaperId);

            return ProcessOutcome.Added;
        }

        /// <summary>
        /// Lưu danh sách tác giả của bài: tạo Author mới nếu chưa có (dedup theo OpenAlexId), rồi link qua PaperAuthors.
        /// </summary>
        /// <param name="paper">OpenAlexPaper - Caller truyền vào - Chứa Authorships (tác giả + thứ tự + affiliation).</param>
        /// <param name="paperId">string - Caller truyền vào - PaperId của ResearchPaper vừa lưu để tạo link.</param>
        private async Task SaveAuthorsAsync(OpenAlexPaper paper, string paperId)
        {
            if (paper.Authorships == null || paper.Authorships.Count == 0) return;

            foreach (var authorship in paper.Authorships)
            {
                if (authorship.Author == null) continue;

                var openAlexAuthorId = authorship.Author.Id?.Replace("https://openalex.org/", "");
                var author = await _dbContext.Authors
                    .FirstOrDefaultAsync(a => a.OpenAlexId == openAlexAuthorId);

                if (author == null)
                {
                    author = new Author
                    {
                        OpenAlexId = openAlexAuthorId,
                        FullName = authorship.Author.DisplayName ?? "Unknown",
                        Affiliation = authorship.Institutions?.FirstOrDefault()?.DisplayName,
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.Authors.Add(author);
                    await _dbContext.SaveChangesAsync();
                }

                _dbContext.PaperAuthors.Add(new PaperAuthor
                {
                    PaperId = paperId,
                    AuthorId = author.AuthorId,
                    AuthorOrder = authorship.AuthorPosition
                });
            }

            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Lưu keyword của bài: tạo Keyword mới nếu chưa có (dedup theo KeywordName), rồi link qua PaperKeywords.
        /// </summary>
        /// <param name="keywords">List&lt;string&gt; - AI trả về - Danh sách keyword đã chuẩn hóa (lowercase-hyphen).</param>
        /// <param name="paperId">string - Caller truyền vào - PaperId của ResearchPaper để tạo link.</param>
        private async Task SaveKeywordsAsync(List<string> keywords, string paperId)
        {
            if (keywords == null || keywords.Count == 0) return;

            foreach (var keywordName in keywords)
            {
                var keyword = await _dbContext.Keywords
                    .FirstOrDefaultAsync(k => k.KeywordName == keywordName);

                if (keyword == null)
                {
                    keyword = new Keyword
                    {
                        KeywordId = Guid.NewGuid().ToString("N")[..20],
                        KeywordName = keywordName,
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.Keywords.Add(keyword);
                    await _dbContext.SaveChangesAsync();
                }

                var linkExists = await _dbContext.PaperKeywords
                    .AnyAsync(pk => pk.PaperId == paperId && pk.KeywordId == keyword.KeywordId);

                if (!linkExists)
                {
                    _dbContext.PaperKeywords.Add(new PaperKeyword
                    {
                        PaperId = paperId,
                        KeywordId = keyword.KeywordId
                    });
                }
            }

            await _dbContext.SaveChangesAsync();
        }
    }
}

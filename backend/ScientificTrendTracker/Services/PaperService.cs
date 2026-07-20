using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.Common;
using ScientificTrendTracker.Models.DTOs.Paper;
using ScientificTrendTracker.Models.Entities;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Triển khai chi tiết các nghiệp vụ quản lý bài báo khoa học dành cho Admin.
    /// </summary>
    public class PaperService : IPaperService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<PaperService> _logger;
        private readonly IOpenAlexService _openAlexService;
        private readonly INotificationService _notificationService;

        public PaperService(AppDbContext dbContext, ILogger<PaperService> logger, IOpenAlexService openAlexService, INotificationService notificationService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _openAlexService = openAlexService;
            _notificationService = notificationService;
        }

        public async Task<PagedResult<PaperAdminDto>> GetPapersForAdminAsync(string search, int? year, string journalId, int page, int pageSize, CancellationToken ct)
        {
            var query = _dbContext.ResearchPapers.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p => p.Title.Contains(search.Trim()));
            }

            if (year.HasValue)
            {
                query = query.Where(p => p.PublicationYear == year.Value);
            }

            if (!string.IsNullOrWhiteSpace(journalId))
            {
                query = query.Where(p => p.JournalId == journalId);
            }

            int totalCount = await query.CountAsync(ct);

            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PaperAdminDto
                {
                    PaperId = p.PaperId,
                    Title = p.Title,
                    PublicationYear = p.PublicationYear,
                    CitationCount = p.CitationCount,
                    JournalName = p.Journal != null ? p.Journal.JournalName : "Unknown",
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync(ct);

            return new PagedResult<PaperAdminDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PaperDetailDto> GetPaperDetailAsync(string paperId, CancellationToken ct)
        {
            var paper = await _dbContext.ResearchPapers
                .AsNoTracking()
                .Include(p => p.Journal)
                .Include(p => p.PaperAuthors).ThenInclude(pa => pa.Author)
                .Include(p => p.PaperKeywords).ThenInclude(pk => pk.Keyword)
                .FirstOrDefaultAsync(p => p.PaperId == paperId, ct);

            if (paper == null) return null;

            return new PaperDetailDto
            {
                PaperId = paper.PaperId,
                OpenAlexId = paper.OpenAlexId,
                Doi = paper.Doi,
                Title = paper.Title,
                PublicationYear = paper.PublicationYear,
                PublicationDate = paper.PublicationDate,
                JournalId = paper.JournalId,
                JournalName = paper.Journal?.JournalName ?? "Không rõ",
                CitationCount = paper.CitationCount,
                SourceUrl = paper.SourceUrl,
                IsAiProcessed = paper.IsAiProcessed,
                CreatedAt = paper.CreatedAt,
                UpdatedAt = paper.UpdatedAt,
                Authors = paper.PaperAuthors.OrderBy(pa => pa.AuthorOrder).Select(pa => pa.Author.FullName).ToList(),
                Keywords = paper.PaperKeywords.Select(pk => pk.Keyword.KeywordName).ToList()
            };
        }

        public async Task<bool> CreatePaperAsync(CreatePaperDto dto, CancellationToken ct)
        {
            // Kiểm tra trùng tiêu đề bài báo
            bool isDuplicate = await _dbContext.ResearchPapers
                .AnyAsync(p => p.Title.ToLower() == dto.Title.Trim().ToLower(), ct);

            if (isDuplicate)
            {
                _logger.LogWarning("Tạo bài báo thất bại: Tiêu đề '{Title}' đã tồn tại.", dto.Title);
                return false;
            }

            var now = DateTime.UtcNow;
            var paperId = Guid.NewGuid().ToString("N")[..20]; // Sinh ID 20 ký tự theo quy chuẩn

            var paper = new ResearchPaper
            {
                PaperId = paperId,
                Title = dto.Title.Trim(),
                Doi = dto.Doi?.Trim(),
                PublicationYear = dto.PublicationYear,
                PublicationDate = dto.PublicationDate,
                SourceUrl = dto.SourceUrl?.Trim(),
                OpenAlexId = string.IsNullOrWhiteSpace(dto.OpenAlexId) ? null : dto.OpenAlexId.Trim(),
                Topic = string.IsNullOrWhiteSpace(dto.Topic) ? null : dto.Topic.Trim(),
                IsAiProcessed = dto.Keywords.Any(), // Đánh dấu là đã xử lý nếu Admin tự truyền keyword vào
                CreatedAt = now
            };

            // 1. Xử lý Tạp chí (Journal)
            if (!string.IsNullOrWhiteSpace(dto.JournalId))
            {
                var journalExists = await _dbContext.Journals.AnyAsync(j => j.JournalId == dto.JournalId, ct);
                if (journalExists)
                {
                    paper.JournalId = dto.JournalId;
                }
            }
            else if (!string.IsNullOrWhiteSpace(dto.JournalName))
            {
                // Kiểm tra xem tên tạp chí có sẵn chưa
                var existingJournal = await _dbContext.Journals
                    .FirstOrDefaultAsync(j => j.JournalName.ToLower() == dto.JournalName.Trim().ToLower(), ct);

                if (existingJournal != null)
                {
                    paper.JournalId = existingJournal.JournalId;
                }
                else
                {
                    // Tạo mới tạp chí
                    var newJournalId = Guid.NewGuid().ToString("N")[..20];
                    var newJournal = new Journal
                    {
                        JournalId = newJournalId,
                        JournalName = dto.JournalName.Trim(),
                        CreatedAt = now
                    };
                    _dbContext.Journals.Add(newJournal);
                    paper.JournalId = newJournalId;
                }
            }

            _dbContext.ResearchPapers.Add(paper);

            // 2. Xử lý tác giả (Authors)
            if (dto.Authors != null && dto.Authors.Any())
            {
                int order = 1;
                foreach (var authorName in dto.Authors)
                {
                    if (string.IsNullOrWhiteSpace(authorName)) continue;

                    var trimmedName = authorName.Trim();
                    var author = await _dbContext.Authors
                        .FirstOrDefaultAsync(a => a.FullName.ToLower() == trimmedName.ToLower(), ct);

                    if (author == null)
                    {
                        author = new Author
                        {
                            FullName = trimmedName,
                            CreatedAt = now
                        };
                        _dbContext.Authors.Add(author);
                        // Save changes tạm để có AuthorId tự sinh cho việc liên kết
                        await _dbContext.SaveChangesAsync(ct);
                    }

                    _dbContext.PaperAuthors.Add(new PaperAuthor
                    {
                        PaperId = paperId,
                        AuthorId = author.AuthorId,
                        AuthorOrder = order++
                    });
                }
            }

            // 3. Xử lý từ khóa (Keywords)
            if (dto.Keywords != null && dto.Keywords.Any())
            {
                foreach (var kwName in dto.Keywords)
                {
                    if (string.IsNullOrWhiteSpace(kwName)) continue;

                    var trimmedKw = kwName.Trim();
                    var keyword = await _dbContext.Keywords
                        .FirstOrDefaultAsync(k => k.KeywordName.ToLower() == trimmedKw.ToLower(), ct);

                    if (keyword == null)
                    {
                        keyword = new Keyword
                        {
                            KeywordId = Guid.NewGuid().ToString("N")[..20],
                            KeywordName = trimmedKw,
                            CreatedAt = now
                        };
                        _dbContext.Keywords.Add(keyword);
                    }

                    _dbContext.PaperKeywords.Add(new PaperKeyword
                    {
                        PaperId = paperId,
                        KeywordId = keyword.KeywordId
                    });
                }
            }

            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Admin tạo bài báo mới thành công: '{Title}' (ID: {PaperId})", paper.Title, paperId);

            // Thông báo cho follower của TÁC GIẢ / TẠP CHÍ khi Admin thêm bài (không chặn nếu lỗi).
            try
            {
                var authorIds = await _dbContext.PaperAuthors
                    .Where(pa => pa.PaperId == paperId)
                    .Select(pa => pa.AuthorId)
                    .ToListAsync(ct);

                // Resolve topic của bài -> TopicId để thông báo cho follower đang theo dõi chủ đề đó.
                // Auto-create ResearchTopic theo tên nếu chưa có (giữ catalog topic nhất quán).
                var topicIds = new List<string>();
                if (!string.IsNullOrWhiteSpace(paper.Topic))
                {
                    var topicName = paper.Topic.Trim();
                    var existingTopic = await _dbContext.ResearchTopics
                        .FirstOrDefaultAsync(t => t.TopicName == topicName, ct);
                    if (existingTopic == null)
                    {
                        existingTopic = new ResearchTopic
                        {
                            TopicId = Guid.NewGuid().ToString("N")[..20],
                            TopicName = topicName,
                            CreatedAt = DateTime.UtcNow
                        };
                        _dbContext.ResearchTopics.Add(existingTopic);
                        await _dbContext.SaveChangesAsync(ct);
                    }
                    // Ghi liên kết Paper <-> Topic qua bảng nối chuẩn (song song với cột Topic string hiện có).
                    _dbContext.PaperTopics.Add(new PaperTopic
                    {
                        PaperId = paperId,
                        TopicId = existingTopic.TopicId
                    });
                    await _dbContext.SaveChangesAsync(ct);
                    topicIds.Add(existingTopic.TopicId);
                }

                await _notificationService.CheckAndPushAsync(new Models.DTOs.NotificationTriggerDto
                {
                    PaperId = paperId,
                    PaperTitle = paper.Title,
                    JournalId = paper.JournalId,
                    TopicIds = topicIds,
                    AuthorIds = authorIds
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi gửi thông báo follower cho bài Admin thêm {PaperId}.", paperId);
            }

            return true;
        }

        public async Task<(bool Success, string Message)> CreatePaperFromLinkAsync(string link, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(link))
                return (false, "Link cannot be empty.");

            // Tự động lấy metadata bài báo từ OpenAlex (title/doi/năm/tạp chí/tác giả/keyword).
            var work = await _openAlexService.FetchSingleWorkAsync(link.Trim());
            if (work == null || string.IsNullOrWhiteSpace(work.Title))
                return (false, "Could not fetch paper data from the link. Please check the OpenAlex link or DOI.");

            DateTime? pubDate = DateTime.TryParse(work.PublicationDate, out var parsed) ? parsed : (DateTime?)null;

            var dto = new CreatePaperDto
            {
                Title = work.Title,
                Doi = string.IsNullOrWhiteSpace(work.Doi) ? null : work.Doi,
                PublicationYear = work.PublicationYear,
                PublicationDate = pubDate,
                SourceUrl = work.Id, // URL OpenAlex của bài báo
                // Lưu OpenAlexId (bỏ prefix URL) để Paper Detail enrich abstract/topic/institutions/OA on-demand
                // — GIỐNG bài sync; nếu không có, màn chi tiết sẽ báo thiếu dữ liệu.
                OpenAlexId = work.Id?.Replace("https://openalex.org/", ""),
                Topic = work.Topic, // chủ đề chính (primary_topic) từ OpenAlex
                JournalName = work.PrimaryLocation?.Source?.DisplayName,
                Authors = (work.Authorships ?? new List<OpenAlexAuthorship>())
                    .Where(a => a.Author != null && !string.IsNullOrWhiteSpace(a.Author.DisplayName))
                    .Select(a => a.Author.DisplayName)
                    .ToList(),
                Keywords = work.Keywords ?? new List<string>()
            };

            var ok = await CreatePaperAsync(dto, ct);
            return ok
                ? (true, $"Paper added: '{work.Title}'.")
                : (false, "Failed to add — the paper may already exist (duplicate title).");
        }

        public async Task<bool> UpdatePaperAsync(string paperId, UpdatePaperDto dto, CancellationToken ct)
        {
            var paper = await _dbContext.ResearchPapers
                .Include(p => p.PaperAuthors)
                .Include(p => p.PaperKeywords)
                .FirstOrDefaultAsync(p => p.PaperId == paperId, ct);

            if (paper == null)
            {
                _logger.LogWarning("Cập nhật thất bại: Không tìm thấy bài báo ID {PaperId}.", paperId);
                return false;
            }

            // Kiểm tra trùng tên với bài khác
            bool isDuplicate = await _dbContext.ResearchPapers
                .AnyAsync(p => p.Title.ToLower() == dto.Title.Trim().ToLower() && p.PaperId != paperId, ct);

            if (isDuplicate)
            {
                _logger.LogWarning("Cập nhật thất bại: Tiêu đề mới '{Title}' đã tồn tại ở bài báo khác.", dto.Title);
                return false;
            }

            var now = DateTime.UtcNow;
            paper.Title = dto.Title.Trim();
            paper.Doi = dto.Doi?.Trim();
            paper.PublicationYear = dto.PublicationYear;
            paper.PublicationDate = dto.PublicationDate;
            paper.SourceUrl = dto.SourceUrl?.Trim();
            paper.UpdatedAt = now;

            // 1. Cập nhật Tạp chí
            if (!string.IsNullOrWhiteSpace(dto.JournalId))
            {
                var journalExists = await _dbContext.Journals.AnyAsync(j => j.JournalId == dto.JournalId, ct);
                if (journalExists)
                {
                    paper.JournalId = dto.JournalId;
                }
            }
            else if (!string.IsNullOrWhiteSpace(dto.JournalName))
            {
                var existingJournal = await _dbContext.Journals
                    .FirstOrDefaultAsync(j => j.JournalName.ToLower() == dto.JournalName.Trim().ToLower(), ct);

                if (existingJournal != null)
                {
                    paper.JournalId = existingJournal.JournalId;
                }
                else
                {
                    var newJournalId = Guid.NewGuid().ToString("N")[..20];
                    var newJournal = new Journal
                    {
                        JournalId = newJournalId,
                        JournalName = dto.JournalName.Trim(),
                        CreatedAt = now
                    };
                    _dbContext.Journals.Add(newJournal);
                    paper.JournalId = newJournalId;
                }
            }

            // 2. Cập nhật tác giả (Xóa cũ - Thêm mới)
            _dbContext.PaperAuthors.RemoveRange(paper.PaperAuthors);
            if (dto.Authors != null && dto.Authors.Any())
            {
                int order = 1;
                foreach (var authorName in dto.Authors)
                {
                    if (string.IsNullOrWhiteSpace(authorName)) continue;

                    var trimmedName = authorName.Trim();
                    var author = await _dbContext.Authors
                        .FirstOrDefaultAsync(a => a.FullName.ToLower() == trimmedName.ToLower(), ct);

                    if (author == null)
                    {
                        author = new Author
                        {
                            FullName = trimmedName,
                            CreatedAt = now
                        };
                        _dbContext.Authors.Add(author);
                        await _dbContext.SaveChangesAsync(ct);
                    }

                    _dbContext.PaperAuthors.Add(new PaperAuthor
                    {
                        PaperId = paperId,
                        AuthorId = author.AuthorId,
                        AuthorOrder = order++
                    });
                }
            }

            // 3. Cập nhật từ khóa (Xóa cũ - Thêm mới)
            _dbContext.PaperKeywords.RemoveRange(paper.PaperKeywords);
            if (dto.Keywords != null && dto.Keywords.Any())
            {
                foreach (var kwName in dto.Keywords)
                {
                    if (string.IsNullOrWhiteSpace(kwName)) continue;

                    var trimmedKw = kwName.Trim();
                    var keyword = await _dbContext.Keywords
                        .FirstOrDefaultAsync(k => k.KeywordName.ToLower() == trimmedKw.ToLower(), ct);

                    if (keyword == null)
                    {
                        keyword = new Keyword
                        {
                            KeywordId = Guid.NewGuid().ToString("N")[..20],
                            KeywordName = trimmedKw,
                            CreatedAt = now
                        };
                        _dbContext.Keywords.Add(keyword);
                    }

                    _dbContext.PaperKeywords.Add(new PaperKeyword
                    {
                        PaperId = paperId,
                        KeywordId = keyword.KeywordId
                    });
                }
            }

            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Admin cập nhật bài báo ID {PaperId} thành công.", paperId);
            return true;
        }

        public async Task<bool> DeletePaperAsync(string paperId, CancellationToken ct)
        {
            var paper = await _dbContext.ResearchPapers.FindAsync(new object[] { paperId }, ct);
            if (paper == null)
            {
                _logger.LogWarning("Xóa bài báo thất bại: Không tìm thấy ID {PaperId}.", paperId);
                return false;
            }

            // Xóa các liên kết trung gian tránh khóa ngoại
            var paperAuthors = await _dbContext.PaperAuthors.Where(pa => pa.PaperId == paperId).ToListAsync(ct);
            _dbContext.PaperAuthors.RemoveRange(paperAuthors);

            var paperKeywords = await _dbContext.PaperKeywords.Where(pk => pk.PaperId == paperId).ToListAsync(ct);
            _dbContext.PaperKeywords.RemoveRange(paperKeywords);

            var citations = await _dbContext.PaperCitations
                .Where(pc => pc.CitingPaperId == paperId || pc.CitedPaperId == paperId)
                .ToListAsync(ct);
            _dbContext.PaperCitations.RemoveRange(citations);

            var bookmarks = await _dbContext.Bookmarks
                .Where(b => b.TargetType == "paper" && b.PaperId == paperId)
                .ToListAsync(ct);
            _dbContext.Bookmarks.RemoveRange(bookmarks);

            _dbContext.ResearchPapers.Remove(paper);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Admin xóa bài báo ID {PaperId} thành công.", paperId);
            return true;
        }
    }
}

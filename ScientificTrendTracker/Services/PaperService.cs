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

        public PaperService(AppDbContext dbContext, ILogger<PaperService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
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
                    JournalName = p.Journal != null ? p.Journal.JournalName : "Không rõ",
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
            return true;
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

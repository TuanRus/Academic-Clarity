using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Sinh citation từ corpus cho LaTeX Writer. Dữ liệu có sẵn: title, year, DOI, journal, authors (có thứ tự).
    /// Volume/issue/pages KHÔNG lưu trong DB → bỏ qua các field đó.
    /// </summary>
    public class LatexCitationService : ILatexCitationService
    {
        private readonly AppDbContext _dbContext;

        public LatexCitationService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<CitationDto> GenerateCitationAsync(string paperId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(paperId)) return null;

            var paper = await _dbContext.ResearchPapers
                .Where(p => p.PaperId == paperId)
                .Include(p => p.Journal)
                .Include(p => p.PaperAuthors).ThenInclude(pa => pa.Author)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);
            if (paper == null) return null;

            var authors = (paper.PaperAuthors ?? new List<Models.Entities.PaperAuthor>())
                .OrderBy(pa => pa.AuthorOrder)
                .Select(pa => pa.Author?.FullName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            var year = paper.PublicationYear?.ToString() ?? "n.d.";
            var key = BuildKey(authors.FirstOrDefault(), paper.Title, paper.PublicationYear);

            // --- BibTeX entry ---
            var bib = new StringBuilder();
            bib.Append("@article{").Append(key).AppendLine(",");
            if (authors.Count > 0)
                bib.Append("  author = {").Append(EscapeLatex(string.Join(" and ", authors))).AppendLine("},");
            bib.Append("  title = {").Append(EscapeLatex(paper.Title)).AppendLine("},");
            if (!string.IsNullOrWhiteSpace(paper.Journal?.JournalName))
                bib.Append("  journal = {").Append(EscapeLatex(paper.Journal.JournalName)).AppendLine("},");
            if (paper.PublicationYear.HasValue)
                bib.Append("  year = {").Append(paper.PublicationYear.Value).AppendLine("},");
            if (!string.IsNullOrWhiteSpace(paper.Doi))
                bib.Append("  doi = {").Append(EscapeLatex(paper.Doi)).AppendLine("},");
            if (!string.IsNullOrWhiteSpace(paper.SourceUrl))
                bib.Append("  url = {").Append(paper.SourceUrl).AppendLine("},");
            bib.Append('}');

            // --- \bibitem cho thebibliography (không cần chạy bibtex khi compile) ---
            var item = new StringBuilder();
            item.Append(@"\bibitem{").Append(key).Append("} ");
            if (authors.Count > 0)
                item.Append(EscapeLatex(FormatAuthorsForBibitem(authors))).Append(". ");
            item.Append(EscapeLatex(paper.Title)).Append(". ");
            if (!string.IsNullOrWhiteSpace(paper.Journal?.JournalName))
                item.Append(@"\textit{").Append(EscapeLatex(paper.Journal.JournalName)).Append("}, ");
            item.Append(year).Append('.');
            if (!string.IsNullOrWhiteSpace(paper.Doi))
                item.Append(" DOI: ").Append(EscapeLatex(paper.Doi)).Append('.');

            return new CitationDto
            {
                BibtexKey = key,
                Bibtex = bib.ToString(),
                Bibitem = item.ToString()
            };
        }

        /// <summary>Key = HọTácGiảĐầu + Năm (bỏ dấu, chỉ giữ chữ/số). Không có tác giả → từ đầu của title.</summary>
        internal static string BuildKey(string firstAuthor, string title, int? year)
        {
            var baseWord = !string.IsNullOrWhiteSpace(firstAuthor)
                ? firstAuthor.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()
                : title?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            var slug = RemoveDiacritics(baseWord ?? "paper");
            slug = new string(slug.Where(char.IsLetterOrDigit).ToArray());
            if (slug.Length == 0) slug = "paper";
            return slug + (year?.ToString() ?? "");
        }

        /// <summary>"A B C" nhiều tác giả → "A B C, D E and F G" (tối đa 3, còn lại "et al.").</summary>
        internal static string FormatAuthorsForBibitem(List<string> authors)
        {
            if (authors.Count == 1) return authors[0];
            if (authors.Count <= 3)
                return string.Join(", ", authors.Take(authors.Count - 1)) + " and " + authors[^1];
            return string.Join(", ", authors.Take(3)) + " et al";
        }

        internal static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            // Đ/đ không phân rã qua FormD → thay thủ công.
            return sb.ToString().Normalize(NormalizationForm.FormC).Replace('Đ', 'D').Replace('đ', 'd');
        }

        /// <summary>Escape các ký tự đặc biệt LaTeX hay gặp trong title/tên journal.</summary>
        internal static string EscapeLatex(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // Backslash gốc đổi sang placeholder trước, vì chuỗi thay thế của nó chứa {} —
            // nếu escape sau bước {} thì không sao, nhưng nếu escape trước thì {} của nó bị escape tiếp.
            const char placeholder = '';
            return s
                .Replace('\\', placeholder)
                .Replace("{", @"\{")
                .Replace("}", @"\}")
                .Replace("&", @"\&")
                .Replace("%", @"\%")
                .Replace("$", @"\$")
                .Replace("#", @"\#")
                .Replace("_", @"\_")
                .Replace("~", @"\textasciitilde{}")
                .Replace("^", @"\textasciicircum{}")
                .Replace(placeholder.ToString(), @"\textbackslash{}");
        }
    }
}

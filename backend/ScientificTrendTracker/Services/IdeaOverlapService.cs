using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScientificTrendTracker.Data;
using ScientificTrendTracker.Models.DTOs;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// B1 — Lexical overlap có TRỌNG SỐ (weighted Jaccard/containment).
    /// Keyword hiếm (xuất hiện ít bài) nặng hơn keyword phổ biến → tránh "trùng" chỉ vì cùng từ đại trà.
    /// Điểm = tổng trọng số keyword chia sẻ / tổng trọng số keyword của abstract đầu vào.
    /// </summary>
    public class IdeaOverlapService : IIdeaOverlapService
    {
        private readonly AppDbContext _dbContext;
        private readonly IIdeaKeywordExtractor _keywordExtractor;

        // Ngưỡng mức cảnh báo sớm (định vị: sàng lọc sơ bộ, KHÔNG phải phát hiện đạo văn).
        private const double HighTier = 0.30;
        private const double MediumTier = 0.15;

        public IdeaOverlapService(AppDbContext dbContext, IIdeaKeywordExtractor keywordExtractor)
        {
            _dbContext = dbContext;
            _keywordExtractor = keywordExtractor;
        }

        public async Task<OverlapResultDto> CheckOverlapAsync(string abstractText, int topN = 10, CancellationToken ct = default)
        {
            var result = new OverlapResultDto();
            if (string.IsNullOrWhiteSpace(abstractText)) return result;

            // 1. Trích keyword: ưu tiên Gemini (2 key luân phiên + failover), fallback Ollama.
            //    Abstract chỉ tồn tại trong memory — KHÔNG lưu DB.
            var keywords = await _keywordExtractor.ExtractKeywordsAsync(abstractText, ct);
            result.ExtractedKeywords = keywords;
            if (keywords.Count == 0) return result;

            // 2. Lấy KeywordId + df (số bài chứa) cho các keyword khớp database.
            var kwInfos = await _dbContext.Keywords
                .Where(k => keywords.Contains(k.KeywordName))
                .Select(k => new { k.KeywordId, k.KeywordName, Df = k.PaperKeywords.Count })
                .ToListAsync();

            result.MatchedKeywordCount = kwInfos.Count;
            if (kwInfos.Count == 0) return result; // không keyword nào có trong DB → không có bài để so

            var totalPapers = await _dbContext.ResearchPapers.CountAsync();

            // IDF mượt: keyword hiếm → trọng số cao. Keyword không có trong DB coi df=0 (hiếm nhất).
            double Idf(int df) => Math.Log((double)(totalPapers + 1) / (df + 1)) + 1.0;

            var dfByName = kwInfos.ToDictionary(k => k.KeywordName, k => k.Df);
            var userWeight = keywords.ToDictionary(
                n => n,
                n => Idf(dfByName.TryGetValue(n, out var df) ? df : 0));
            var denom = userWeight.Values.Sum();
            if (denom <= 0) return result;

            // 3. Candidate = các bài chia sẻ ≥1 keyword (không quét toàn corpus).
            var kwIdToName = kwInfos.ToDictionary(k => k.KeywordId, k => k.KeywordName);
            var matchedKwIds = kwInfos.Select(k => k.KeywordId).ToList();

            var links = await _dbContext.PaperKeywords
                .Where(pk => matchedKwIds.Contains(pk.KeywordId))
                .Select(pk => new { pk.PaperId, pk.KeywordId })
                .ToListAsync();

            // 4. Tính điểm weighted cho từng bài, lấy topN.
            var scored = links
                .GroupBy(l => l.PaperId)
                .Select(g =>
                {
                    var shared = g.Select(x => kwIdToName[x.KeywordId]).Distinct().ToList();
                    var num = shared.Sum(n => userWeight[n]);
                    return new { PaperId = g.Key, Shared = shared, Score = num / denom };
                })
                .OrderByDescending(x => x.Score)
                .Take(topN)
                .ToList();

            if (scored.Count == 0) return result;

            // 5. Lấy thông tin hiển thị của các bài top.
            var paperIds = scored.Select(x => x.PaperId).ToList();
            var papers = await _dbContext.ResearchPapers
                .Where(p => paperIds.Contains(p.PaperId))
                .Include(p => p.Journal)
                .ToDictionaryAsync(p => p.PaperId);

            result.Matches = scored.Select(s =>
            {
                papers.TryGetValue(s.PaperId, out var p);
                return new OverlapMatchDto
                {
                    PaperId = s.PaperId,
                    Title = p?.Title,
                    Year = p?.PublicationYear,
                    CitationCount = p?.CitationCount ?? 0,
                    JournalName = p?.Journal?.JournalName,
                    SourceUrl = p?.SourceUrl,
                    SharedKeywords = s.Shared,
                    Score = Math.Round(s.Score, 3),
                    Tier = s.Score >= HighTier ? "high" : s.Score >= MediumTier ? "medium" : "low"
                };
            }).ToList();

            // 6. AI phân tích trùng Ý TƯỞNG (ngữ nghĩa) trên các bài top, rồi KẾT HỢP với bằng chứng keyword.
            await EnrichWithAiAsync(abstractText, result, ct);

            return result;
        }

        /// <summary>
        /// Gọi AI (Gemini) đọc abstract + các bài trùng keyword → nhận định trùng Ý TƯỞNG cho từng bài
        /// + đánh giá tổng hợp + mức rủi ro. Kết hợp với tier keyword để ra FinalVerdict. Lỗi/không có key → bỏ qua êm.
        /// </summary>
        private async Task EnrichWithAiAsync(string abstractText, OverlapResultDto result, CancellationToken ct)
        {
            var top = result.Matches.Take(5).ToList();
            if (top.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine("You are a research-integrity assistant helping an author self-check the novelty of their idea.");
            sb.AppendLine("The author's abstract:");
            sb.AppendLine("\"\"\"");
            sb.AppendLine(abstractText.Length > 4000 ? abstractText[..4000] : abstractText);
            sb.AppendLine("\"\"\"");
            sb.AppendLine();
            sb.AppendLine("Existing papers that share keywords with it:");
            for (int i = 0; i < top.Count; i++)
                sb.AppendLine($"[{i + 1}] {top[i].Title} (shared keywords: {string.Join(", ", top[i].SharedKeywords)})");
            sb.AppendLine();
            sb.AppendLine("Judge whether the author's CORE IDEA overlaps with each paper — semantic overlap of the research idea, not just shared words. Then give one overall judgement that COMBINES the shared-keyword evidence with your semantic reading.");
            sb.AppendLine("Return ONLY minified JSON, no markdown, of the exact shape:");
            sb.AppendLine("{\"overallRisk\":\"low|medium|high\",\"assessment\":\"2-4 sentences in English\",\"papers\":[{\"index\":1,\"note\":\"one concise sentence why it overlaps or not\"}]}");

            var raw = await _keywordExtractor.AnalyzeAsync(sb.ToString(), ct);
            if (string.IsNullOrWhiteSpace(raw)) return;

            try
            {
                var start = raw.IndexOf('{');
                var end = raw.LastIndexOf('}');
                if (start < 0 || end <= start) return;
                using var doc = JsonDocument.Parse(raw.Substring(start, end - start + 1));
                var root = doc.RootElement;

                if (root.TryGetProperty("overallRisk", out var riskEl))
                    result.AiRisk = NormalizeRisk(riskEl.GetString());
                if (root.TryGetProperty("assessment", out var asmEl))
                    result.AiAssessment = asmEl.GetString();

                if (root.TryGetProperty("papers", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arr.EnumerateArray())
                    {
                        if (!item.TryGetProperty("index", out var idxEl)) continue;
                        var idx = idxEl.ValueKind == JsonValueKind.Number ? idxEl.GetInt32()
                                : int.TryParse(idxEl.GetString(), out var n) ? n : 0;
                        if (idx >= 1 && idx <= top.Count && item.TryGetProperty("note", out var noteEl))
                            top[idx - 1].AiNote = noteEl.GetString();
                    }
                }
            }
            catch
            {
                // AI trả JSON hỏng → giữ kết quả keyword, không set narrative.
                return;
            }

            // Kết luận cuối = mức CAO HƠN giữa tier keyword của bài top và mức rủi ro AI.
            var keywordTop = result.Matches.Count > 0 ? result.Matches[0].Tier : "low";
            result.FinalVerdict = MaxSeverity(keywordTop, result.AiRisk ?? "low");
        }

        private static string NormalizeRisk(string s)
        {
            s = (s ?? "").Trim().ToLowerInvariant();
            return s is "low" or "medium" or "high" ? s : "low";
        }

        private static int Rank(string tier) => tier switch { "high" => 3, "medium" => 2, _ => 1 };
        private static string MaxSeverity(string a, string b) => Rank(a) >= Rank(b) ? NormalizeRisk(a) : NormalizeRisk(b);
    }
}

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

        // Số keyword TRÙNG tối thiểu để đạt "độ phủ" đầy đủ. Trùng ít hơn → điểm bị phạt theo tỉ lệ.
        // Mục đích: tránh "trùng 1/3 keyword = 33% = High" phi thực tế khi abstract có ít keyword.
        private const int MinSharedForFullCoverage = 3;

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

            // 2. Nạp TOÀN BỘ catalog keyword (id, name) + df để khớp LINH HOẠT theo từ chung,
            //    không chỉ khớp đúng chữ. Tránh trường hợp abstract dùng "cnn"/"image augmentation
            //    algorithms" mà bài lưu "convolutional neural network"/"image data augmentation".
            var catalog = await _dbContext.Keywords
                .AsNoTracking()
                .Select(k => new { k.KeywordId, k.KeywordName })
                .ToListAsync();
            var dfById = await _dbContext.PaperKeywords
                .GroupBy(pk => pk.KeywordId)
                .Select(g => new { KeywordId = g.Key, Df = g.Count() })
                .ToDictionaryAsync(x => x.KeywordId, x => x.Df);

            var totalPapers = await _dbContext.ResearchPapers.CountAsync();
            double Idf(int df) => Math.Log((double)(totalPapers + 1) / (df + 1)) + 1.0;

            // Cache token của keyword catalog (tính 1 lần) để khớp nhanh.
            var catalogToks = catalog.ToDictionary(k => k.KeywordId, k => Tokenize(k.KeywordName));

            // Với mỗi keyword người dùng: tìm tập keyword-DB tương đương + df ĐẠI DIỆN (khớp sát nhất;
            // nếu nhiều cái ngang nhau lấy df lớn hơn = nghĩa phổ biến, tránh thổi phồng trọng số).
            var matchIdsByUser = new Dictionary<string, HashSet<string>>();
            var userWeight = new Dictionary<string, double>();
            foreach (var u in keywords)
            {
                var ut = Tokenize(u);
                var set = new HashSet<string>();
                int repDf = -1; double bestSim = -1;
                foreach (var c in catalog)
                {
                    bool exact = c.KeywordName == u;
                    var ctoks = catalogToks[c.KeywordId];
                    if (!exact && !ConceptMatch(ut, ctoks)) continue;
                    set.Add(c.KeywordId);
                    int df = dfById.TryGetValue(c.KeywordId, out var d) ? d : 0;
                    int inter = ut.Count(x => ctoks.Contains(x));
                    int uni = ut.Count + ctoks.Count - inter;
                    double sim = exact ? 2.0 : (uni > 0 ? (double)inter / uni : 0);
                    if (sim > bestSim || (Math.Abs(sim - bestSim) < 1e-9 && df > repDf)) { bestSim = sim; repDf = df; }
                }
                matchIdsByUser[u] = set;
                userWeight[u] = Idf(repDf < 0 ? 0 : repDf); // không khớp gì → df=0 (hiếm nhất) → vẫn vào mẫu số
            }

            result.MatchedKeywordCount = matchIdsByUser.Count(kv => kv.Value.Count > 0);
            var denom = userWeight.Values.Sum();
            if (denom <= 0) return result;

            // 3. Map keyword-DB → keyword người dùng (concept). 1 keyword-DB có thể thuộc nhiều concept.
            var kwIdToUsers = new Dictionary<string, List<string>>();
            foreach (var u in keywords)
                foreach (var id in matchIdsByUser[u])
                {
                    if (!kwIdToUsers.TryGetValue(id, out var lst)) { lst = new(); kwIdToUsers[id] = lst; }
                    lst.Add(u);
                }
            if (kwIdToUsers.Count == 0) return result; // không concept nào khớp DB

            var matchedKwIds = kwIdToUsers.Keys.ToList();
            var links = await _dbContext.PaperKeywords
                .Where(pk => matchedKwIds.Contains(pk.KeywordId))
                .Select(pk => new { pk.PaperId, pk.KeywordId })
                .ToListAsync();

            // 4. Điểm mỗi bài = (tổng trọng số CONCEPT bài chạm / denom) × coverage.
            //    coverage phạt bài trùng ÍT concept (vd 1/3) để không bị đẩy lên High phi thực tế.
            var scored = links
                .GroupBy(l => l.PaperId)
                .Select(g =>
                {
                    var concepts = g.SelectMany(x => kwIdToUsers[x.KeywordId]).Distinct().ToList();
                    var weighted = concepts.Sum(u => userWeight[u]) / denom;
                    var coverage = Math.Min(1.0, (double)concepts.Count / MinSharedForFullCoverage);
                    return new { PaperId = g.Key, Shared = concepts, Score = weighted * coverage };
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
                    Tier = TierOf(s.Score, s.Shared.Count)
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

        /// <summary>
        /// Xếp mức cảnh báo theo điểm + SỐ concept trùng.
        /// - High: trùng ≥3 concept (đủ tự tin) HOẶC 2 concept nhưng điểm rất cao (≥0.55).
        /// - 1 concept: KHÔNG bao giờ high (tránh cảnh báo giả khi chỉ trùng đúng 1 từ).
        /// </summary>
        private static string TierOf(double score, int sharedCount)
        {
            if ((score >= HighTier && sharedCount >= 3) || (score >= 0.55 && sharedCount >= 2)) return "high";
            if (score >= MediumTier && sharedCount >= 2) return "medium";
            return "low";
        }

        // Stopword loại khỏi token khi so khớp khái niệm (từ chung/generic không mang nghĩa phân biệt).
        private static readonly HashSet<string> _tokenStop = new()
        {
            "deep","based","using","via","approach","method","algorithm","model","technique","system",
            "a","an","the","for","of","and","in","on","to","with","from","by",
            "survey","review","comprehensive","general","novel","new","study","analysis"
        };

        /// <summary>Tách keyword thành tập token: lowercase, bỏ ký tự lạ, bỏ stopword, chuẩn hoá số nhiều đơn giản.</summary>
        private static HashSet<string> Tokenize(string s)
        {
            var set = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(s)) return set;
            var raw = new string(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray())
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var t0 in raw)
            {
                if (_tokenStop.Contains(t0)) continue;
                var t = t0.Length > 3 && t0.EndsWith("s") ? t0[..^1] : t0; // số nhiều: bỏ 's' cuối
                if (t.Length >= 2 && !_tokenStop.Contains(t)) set.Add(t);
            }
            return set;
        }

        /// <summary>
        /// Hai keyword "khớp khái niệm" nếu chia sẻ đủ từ: Jaccard token ≥ 0.5 HOẶC tập nhỏ nằm trọn
        /// trong tập lớn (containment). Giúp "image augmentation algorithms" ≈ "image data augmentation".
        /// </summary>
        private static bool ConceptMatch(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 || b.Count == 0) return false;
            int inter = a.Count(x => b.Contains(x));
            if (inter == 0) return false;
            int uni = a.Count + b.Count - inter;
            double jac = (double)inter / uni;
            return jac >= 0.5 || inter == Math.Min(a.Count, b.Count);
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

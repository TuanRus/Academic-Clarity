using System.Net;
using System.Text;
using System.Text.Json;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Trích keyword cho Idea Check bằng Google Gemini với NHIỀU API key luân phiên (round-robin) + failover.
    /// - Mỗi request bắt đầu từ key kế tiếp → 2 key thay phiên nhau gánh tải.
    /// - Nếu 1 key bị 429 (rate-limit)/lỗi → thử key còn lại.
    /// - Nếu CẢ các key đều thất bại → fallback sang AI local (Ollama) qua IKeywordExtractionService.
    /// </summary>
    public class GeminiIdeaKeywordExtractor : IIdeaKeywordExtractor
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly IKeywordExtractionService _ollamaFallback;
        private readonly ILogger<GeminiIdeaKeywordExtractor> _logger;

        // Con trỏ xoay vòng key dùng chung toàn app (thread-safe) → các key thay phiên nhau khởi đầu.
        private static int _rotation = -1;

        private const string PromptTemplate =
            "You are a Computer Science research assistant.\n" +
            "Extract the most important technical keywords from the abstract below.\n" +
            "Rules:\n" +
            "- Return ONLY a JSON array of strings, no explanation.\n" +
            "- Maximum 8 keywords.\n" +
            "- Specific technical terms only (not generic words like \"study\", \"result\", \"method\").\n" +
            "- All lowercase, use spaces for multi-word terms (e.g. \"machine learning\").\n" +
            "Abstract:\n{0}\n" +
            "Response format (JSON array only): [\"keyword1\", \"keyword2\"]";

        public GeminiIdeaKeywordExtractor(
            HttpClient http,
            IConfiguration config,
            IKeywordExtractionService ollamaFallback,
            ILogger<GeminiIdeaKeywordExtractor> logger)
        {
            _http = http;
            _config = config;
            _ollamaFallback = ollamaFallback;
            _logger = logger;
        }

        public async Task<List<string>> ExtractKeywordsAsync(string abstractText, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(abstractText)) return new List<string>();

            var keys = (_config.GetSection("Gemini:ApiKeys").Get<string[]>() ?? Array.Empty<string>())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .ToArray();

            if (keys.Length > 0)
            {
                var model = _config["Gemini:Model"] ?? "gemini-2.0-flash";
                var baseUrl = (_config["Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com").TrimEnd('/');
                var timeoutSec = _config.GetValue("Gemini:TimeoutSeconds", 30);

                // Xoay vòng: mỗi request bắt đầu từ key kế tiếp để 2 key thay phiên gánh tải.
                int start = (int)((uint)System.Threading.Interlocked.Increment(ref _rotation) % (uint)keys.Length);

                for (int i = 0; i < keys.Length; i++)
                {
                    var key = keys[(start + i) % keys.Length];
                    var (ok, keywords) = await TryGeminiAsync(baseUrl, model, key, abstractText, timeoutSec, ct);
                    if (ok) return keywords;
                }

                _logger.LogWarning("Cả {N} Gemini key đều thất bại/limited → fallback Ollama cho Idea Check.", keys.Length);
            }
            else
            {
                _logger.LogInformation("Không cấu hình Gemini:ApiKeys → dùng thẳng Ollama cho Idea Check.");
            }

            // Fallback: AI local (Ollama). Nếu Ollama cũng tắt → trả rỗng (service tự log).
            return await _ollamaFallback.ExtractKeywordsAsync(abstractText, "(pasted abstract)");
        }

        /// <summary>
        /// Sinh văn bản tự do bằng Gemini (2 key luân phiên) cho phân tích trùng ý tưởng.
        /// Trả text thô; null nếu không có key hoặc mọi key thất bại (KHÔNG fallback Ollama cho phần narrative).
        /// </summary>
        public async Task<string> AnalyzeAsync(string prompt, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return null;

            var keys = (_config.GetSection("Gemini:ApiKeys").Get<string[]>() ?? Array.Empty<string>())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .ToArray();
            if (keys.Length == 0)
            {
                _logger.LogInformation("Không cấu hình Gemini:ApiKeys → phân tích trùng ý tưởng bằng Ollama.");
                return await _ollamaFallback.CompleteAsync(prompt, ct);
            }

            var model = _config["Gemini:Model"] ?? "gemini-2.0-flash";
            var baseUrl = (_config["Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com").TrimEnd('/');
            var timeoutSec = _config.GetValue("Gemini:TimeoutSeconds", 30);

            int start = (int)((uint)System.Threading.Interlocked.Increment(ref _rotation) % (uint)keys.Length);
            for (int i = 0; i < keys.Length; i++)
            {
                var key = keys[(start + i) % keys.Length];
                var (ok, text) = await CallGeminiRawAsync(baseUrl, model, key, prompt, 600, timeoutSec, ct);
                if (ok) return text;
            }

            _logger.LogWarning("Cả {N} Gemini key đều thất bại → fallback Ollama cho phân tích trùng ý tưởng.", keys.Length);
            return await _ollamaFallback.CompleteAsync(prompt, ct);
        }

        /// <summary>Gọi Gemini 1 key với prompt bất kỳ, trả (ok, rawText). ok=false khi 429/lỗi/timeout.</summary>
        private async Task<(bool ok, string text)> CallGeminiRawAsync(
            string baseUrl, string model, string apiKey, string prompt, int maxTokens, int timeoutSec, CancellationToken ct)
        {
            var url = $"{baseUrl}/v1beta/models/{model}:generateContent?key={apiKey}";
            var payload = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { temperature = 0.2, maxOutputTokens = maxTokens }
            };

            using var reqCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            reqCts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));
            try
            {
                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var resp = await _http.PostAsync(url, content, reqCts.Token);
                if (resp.StatusCode == HttpStatusCode.TooManyRequests) return (false, null);
                if (!resp.IsSuccessStatusCode) return (false, null);

                var json = await resp.Content.ReadAsStringAsync(reqCts.Token);
                var text = ExtractText(json);
                return string.IsNullOrWhiteSpace(text) ? (false, null) : (true, text);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Gemini key ...{Tail} timeout (analyze) sau {Sec}s.", Tail(apiKey), timeoutSec);
                return (false, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini key ...{Tail} lỗi khi gọi API (analyze).", Tail(apiKey));
                return (false, null);
            }
        }

        /// <summary>Gọi Gemini với 1 key. Trả (ok, keywords). ok=false khi 429/lỗi/timeout để caller thử key khác.</summary>
        private async Task<(bool ok, List<string> keywords)> TryGeminiAsync(
            string baseUrl, string model, string apiKey, string abstractText, int timeoutSec, CancellationToken ct)
        {
            var url = $"{baseUrl}/v1beta/models/{model}:generateContent?key={apiKey}";
            var payload = new
            {
                contents = new[] { new { parts = new[] { new { text = string.Format(PromptTemplate, abstractText) } } } },
                generationConfig = new { temperature = 0, maxOutputTokens = 256 }
            };

            // Timeout riêng cho từng key, đồng thời tôn trọng CancellationToken của request gốc.
            using var reqCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            reqCts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

            try
            {
                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var resp = await _http.PostAsync(url, content, reqCts.Token);

                if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("Gemini key ...{Tail} bị rate-limit (429), chuyển key khác.", Tail(apiKey));
                    return (false, null);
                }
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Gemini key ...{Tail} lỗi HTTP {Code}.", Tail(apiKey), (int)resp.StatusCode);
                    return (false, null);
                }

                var json = await resp.Content.ReadAsStringAsync(reqCts.Token);
                var text = ExtractText(json);
                var keywords = ParseKeywords(text);
                return keywords.Count > 0 ? (true, keywords) : (false, null);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Gemini key ...{Tail} timeout sau {Sec}s.", Tail(apiKey), timeoutSec);
                return (false, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gemini key ...{Tail} lỗi khi gọi API.", Tail(apiKey));
                return (false, null);
            }
        }

        private static string Tail(string key) => string.IsNullOrEmpty(key) || key.Length <= 4 ? "****" : key[^4..];

        /// <summary>Bóc text kết quả từ JSON Gemini: candidates[0].content.parts[0].text.</summary>
        private static string ExtractText(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("candidates", out var cands) && cands.GetArrayLength() > 0)
            {
                var first = cands[0];
                if (first.TryGetProperty("content", out var c)
                    && c.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0
                    && parts[0].TryGetProperty("text", out var t))
                    return t.GetString();
            }
            return null;
        }

        /// <summary>Tách JSON array trong text, chuẩn hoá + lọc keyword giống toàn hệ thống, tối đa 8.</summary>
        private static List<string> ParseKeywords(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
            var start = raw.IndexOf('[');
            var end = raw.LastIndexOf(']');
            if (start < 0 || end <= start) return new List<string>();

            try
            {
                var arr = JsonSerializer.Deserialize<List<string>>(raw.Substring(start, end - start + 1));
                return arr?
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Select(KeywordNormalizer.Normalize)
                    .Where(k => k.Length >= 3 && !KeywordStopwords.IsGeneric(k))
                    .Distinct()
                    .Take(8)
                    .ToList() ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}

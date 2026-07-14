using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    /// <summary>
    /// Trích keyword bằng AI local (Ollama / OpenAI-compatible) đọc từ section "AiProviders".
    /// Thử lần lượt từng provider; provider đầu trả keyword thì dùng luôn. Không có cloud/quota/rate-limit.
    /// </summary>
    public class KeywordExtractionService : IKeywordExtractionService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<KeywordExtractionService> _logger;

        private const string PromptTemplate = """
            You are a Computer Science research assistant.
            Extract the most important technical keywords from the paper abstract below.

            Rules:
            - Return ONLY a JSON array of strings, no explanation.
            - Maximum 8 keywords.
            - Keywords must be specific technical terms (not generic words like "study", "result", "method").
            - All lowercase, use spaces for multi-word terms (e.g. "machine learning", "large language model").
            - Match the style of a controlled vocabulary: concise canonical noun phrases (1-4 words),
              singular, no acronyms-only, no author names, no metrics/numbers.
            - Focus on: algorithms, architectures, tasks, datasets, domains.
            {{$seed}}
            Paper title: {{$title}}
            Abstract: {{$abstract}}

            Response format (JSON array only):
            ["keyword1", "keyword2", "keyword3"]
            """;

        public KeywordExtractionService(IConfiguration configuration, ILogger<KeywordExtractionService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<string>> ExtractKeywordsAsync(string @abstract, string paperTitle, IReadOnlyList<string> seedKeywords = null)
        {
            if (string.IsNullOrWhiteSpace(@abstract))
            {
                _logger.LogWarning("Abstract rỗng cho paper '{Title}', bỏ qua keyword extraction.", paperTitle);
                return new List<string>();
            }

            var seed = BuildSeedInstruction(seedKeywords);

            // Thử lần lượt từng provider local trong "AiProviders" (thường chỉ 1: Ollama).
            foreach (var p in LoadOpenAiProviders())
            {
                if (string.IsNullOrEmpty(p.BaseUrl) || string.IsNullOrEmpty(p.Model)) continue;
                try
                {
                    var result = await InvokeOpenAICompatibleAsync(p.BaseUrl, p.ApiKey, p.Model, @abstract, paperTitle, seed);
                    if (result.Count > 0) return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi AI provider {Provider} model={Model}: {Message}", p.Name, p.Model, ex.Message);
                }
            }

            _logger.LogWarning("AI không trích được keyword cho paper '{Title}'.", paperTitle);
            return new List<string>();
        }

        /// <inheritdoc/>
        public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return null;

            foreach (var p in LoadOpenAiProviders())
            {
                if (string.IsNullOrEmpty(p.BaseUrl) || string.IsNullOrEmpty(p.Model)) continue;
                try
                {
                    var kernel = Kernel.CreateBuilder()
                        .AddOpenAIChatCompletion(
                            modelId: p.Model,
                            apiKey: p.ApiKey,
                            httpClient: new HttpClient { BaseAddress = new Uri(p.BaseUrl), Timeout = TimeSpan.FromSeconds(120) })
                        .Build();

                    var settings = new OpenAIPromptExecutionSettings { Temperature = 0.2, MaxTokens = 600 };
                    var result = await kernel.InvokePromptAsync(prompt, new KernelArguments(settings), cancellationToken: ct);
                    var text = result.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "CompleteAsync lỗi provider {Provider}: {Message}", p.Name, ex.Message);
                }
            }
            return null;
        }

        /// <summary>
        /// Dựng đoạn hướng dẫn "seed" từ keyword OpenAlex sẵn có để AI bám controlled vocabulary.
        /// Rỗng nếu không có seed → prompt hoạt động như cũ.
        /// </summary>
        private static string BuildSeedInstruction(IReadOnlyList<string> seedKeywords)
        {
            if (seedKeywords == null || seedKeywords.Count == 0) return string.Empty;
            var cleaned = seedKeywords
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList();
            if (cleaned.Count == 0) return string.Empty;

            var list = string.Join(", ", cleaned);
            return $"""

                Reference keywords (from OpenAlex's controlled vocabulary for THIS paper): [{list}].
                Treat these as the gold-standard style and vocabulary:
                - REUSE these reference terms when they fit the abstract (keep them in the output).
                - You MAY add a few extra specific technical terms ONLY if they are clearly central to the
                  paper and follow the SAME canonical style as the references.
                - Do NOT output generic, vague, or off-topic words. Stay close to this vocabulary.
                """;
        }

        /// <summary>
        /// Đọc danh sách provider OpenAI-compatible từ section "AiProviders".
        /// Mỗi item: { Name, BaseUrl, ApiKey, Model }.
        /// </summary>
        private List<(string Name, string BaseUrl, string ApiKey, string Model)> LoadOpenAiProviders()
        {
            var list = new List<(string, string, string, string)>();
            foreach (var c in _configuration.GetSection("AiProviders").GetChildren())
            {
                var url = c["BaseUrl"];
                var model = c["Model"];
                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(model)) continue;
                list.Add((c["Name"] ?? url, url, c["ApiKey"] ?? "x", model));
            }
            return list;
        }

        private async Task<List<string>> InvokeOpenAICompatibleAsync(string baseUrl, string apiKey, string model, string @abstract, string paperTitle, string seed)
        {
            var kernel = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(
                    modelId: model,
                    apiKey: apiKey,
                    httpClient: new HttpClient { BaseAddress = new Uri(baseUrl) })
                .Build();

            var function = kernel.CreateFunctionFromPrompt(PromptTemplate);

            // Tất định: cùng abstract → cùng keyword (ổn định, lặp lại được khi demo/đánh giá).
            var settings = new OpenAIPromptExecutionSettings { Temperature = 0, TopP = 1, Seed = 42 };

            var result = await kernel.InvokeAsync(function, new KernelArguments(settings)
            {
                ["title"] = paperTitle,
                ["abstract"] = @abstract,
                ["seed"] = seed ?? string.Empty
            });

            return ParseKeywords(result.ToString().Trim(), paperTitle);
        }

        /// <summary>Loại keyword rác: quá ngắn, chỉ chứa số/dấu, hoặc là từ generic (stoplist dùng chung).</summary>
        private static bool IsQualityKeyword(string kw)
        {
            if (string.IsNullOrWhiteSpace(kw) || kw.Length < 3) return false;
            if (kw.All(c => char.IsDigit(c) || c == '-' || c == ' ')) return false;
            if (KeywordStopwords.IsGeneric(kw)) return false;
            return true;
        }

        private List<string> ParseKeywords(string rawResponse, string paperTitle)
        {
            try
            {
                // Tách phần JSON array ra khỏi response (model đôi khi thêm text thừa)
                var start = rawResponse.IndexOf('[');
                var end = rawResponse.LastIndexOf(']');

                if (start == -1 || end == -1 || end <= start)
                {
                    _logger.LogWarning("AI trả về format không hợp lệ cho '{Title}': {Response}", paperTitle, rawResponse);
                    return new List<string>();
                }

                var jsonArray = rawResponse.Substring(start, end - start + 1);
                var keywords = System.Text.Json.JsonSerializer.Deserialize<List<string>>(jsonArray);

                return keywords?
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    // Chuẩn hóa giống toàn hệ thống: lowercase + dấu cách (tránh "big data" vs "big-data")
                    .Select(KeywordNormalizer.Normalize)
                    .Where(IsQualityKeyword)
                    .Distinct()
                    .Take(8)
                    .ToList() ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi parse keyword JSON cho '{Title}': {Message}", paperTitle, ex.Message);
                return new List<string>();
            }
        }
    }
}

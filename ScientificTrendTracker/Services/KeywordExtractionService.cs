#pragma warning disable SKEXP0070 // Gemini connector là experimental trong SK 1.x

using System.Collections.Concurrent;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    public class KeywordExtractionService : IKeywordExtractionService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<KeywordExtractionService> _logger;

        // Circuit-breaker: khi 1 provider trả 429 (hết quota ngày), tắt nó đến thời điểm lưu ở đây.
        // static để trạng thái tồn tại xuyên suốt process — service là Scoped, mỗi paper tạo instance mới.
        private static readonly ConcurrentDictionary<string, DateTime> _cooldownUntilUtc = new();

        // Model được đọc từ config theo từng key

        private const string PromptTemplate = """
            You are a Computer Science research assistant.
            Extract the most important technical keywords from the paper abstract below.

            Rules:
            - Return ONLY a JSON array of strings, no explanation.
            - Maximum 8 keywords.
            - Keywords must be specific technical terms (not generic words like "study", "result", "method").
            - All lowercase, use hyphens for multi-word terms (e.g. "machine-learning", "large-language-model").
            - Focus on: algorithms, architectures, tasks, datasets, domains.

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

        /// <summary>
        /// Gửi abstract bài báo lên Gemini AI để trích xuất keyword kỹ thuật đặc trưng.
        /// Tự động fallback sang ApiKey2 nếu ApiKey1 bị rate limit (HTTP 429).
        /// Abstract chỉ tồn tại trong memory, KHÔNG được lưu vào DB.
        /// </summary>
        /// <param name="abstract">
        /// string - OpenAlexService.ReconstructAbstract() trả về -
        /// Full text abstract đã ghép từ inverted index. Null hoặc rỗng sẽ trả về list rỗng ngay.
        /// </param>
        /// <param name="paperTitle">
        /// string - OpenAlexPaper.Title từ OpenAlex API -
        /// Tiêu đề bài báo, bổ sung context giúp Gemini extract chính xác hơn.
        /// </param>
        /// <returns>
        /// List&lt;string&gt; - Danh sách keyword kỹ thuật do Gemini trích xuất.
        /// Mỗi keyword là:
        /// - (string): Lowercase, hyphen cho cụm từ (vd: "machine-learning", "large-language-model")
        /// - Tối đa 8 keyword mỗi bài
        /// - Chỉ thuật ngữ kỹ thuật, loại bỏ từ generic ("study", "result", "method")
        /// Trả về list rỗng nếu abstract null, Gemini lỗi, hoặc tất cả API key đều thất bại.
        /// </returns>
        public async Task<List<string>> ExtractKeywordsAsync(string @abstract, string paperTitle)
        {
            if (string.IsNullOrWhiteSpace(@abstract))
            {
                _logger.LogWarning("Abstract rỗng cho paper '{Title}', bỏ qua keyword extraction.", paperTitle);
                return new List<string>();
            }

            // Theo dõi: có provider nào thực sự PHẢN HỒI không (HTTP 200, dù trả 0 keyword)?
            // Nếu mọi provider đều cooldown/429 → ném AllProvidersExhaustedException để bulk processor dừng.
            var anyProviderResponded = false;

            // Thử lần lượt từng Gemini key (đọc từ mảng Gemini:Keys) → Groq (fallback cuối)
            var geminiKeys = LoadGeminiKeys();

            foreach (var (config, index) in geminiKeys.Select((c, i) => (c, i)))
            {
                if (string.IsNullOrEmpty(config.ApiKey)) continue;
                var provider = $"gemini-key{index + 1}";
                if (IsInCooldown(provider)) continue; // Bỏ qua key đã hết quota — không bắn request rác

                try
                {
                    var result = await InvokeGeminiAsync(config.ApiKey, config.Model, @abstract, paperTitle);
                    anyProviderResponded = true;
                    if (result.Count > 0) return result;
                }
                catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("quota") || ex.Message.Contains("RESOURCE_EXHAUSTED"))
                {
                    // Phân biệt giới hạn NGÀY (PerDay) vs PHÚT (PerMinute/RPM/TPM)
                    var isDailyLimit = ex.Message.Contains("PerDay") || ex.Message.Contains("per day");
                    if (isDailyLimit)
                    {
                        TripBreakerUntilNextUtcMidnight(provider);
                        _logger.LogWarning("{Provider} hết quota NGÀY (429), tắt đến 00:00 UTC.", provider);
                    }
                    else
                    {
                        // Chạm RPM/TPM → chỉ cần nghỉ ngắn, không phí key cả ngày
                        _cooldownUntilUtc[provider] = DateTime.UtcNow.AddSeconds(60);
                        _logger.LogWarning("{Provider} chạm rate limit phút (429), tắt 60s.", provider);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi {Provider} model={Model}: {Message}", provider, config.Model, ex.Message);
                }
            }

            // Fallback: lần lượt từng Groq key (đọc từ mảng Groq:Keys) qua OpenAI-compatible connector
            var groqKeys = LoadGroqKeys();

            foreach (var (config, index) in groqKeys.Select((c, i) => (c, i)))
            {
                if (string.IsNullOrEmpty(config.ApiKey)) continue;
                var provider = $"groq-key{index + 1}";
                if (IsInCooldown(provider)) continue;

                try
                {
                    var result = await InvokeGroqAsync(config.ApiKey, config.Model, @abstract, paperTitle);
                    anyProviderResponded = true;
                    if (result.Count > 0) return result;
                }
                catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("rate"))
                {
                    // Groq daily limit reset 00:00 UTC. Phân biệt giới hạn ngày vs phút.
                    var isDailyLimit = ex.Message.Contains("per day") || ex.Message.Contains("RPD")
                        || ex.Message.Contains("daily") || ex.Message.Contains("tokens per day") || ex.Message.Contains("TPD");
                    if (isDailyLimit)
                    {
                        TripBreakerUntilNextUtcMidnight(provider);
                        _logger.LogWarning("{Provider} hết quota NGÀY (429), tắt đến 00:00 UTC.", provider);
                    }
                    else
                    {
                        _cooldownUntilUtc[provider] = DateTime.UtcNow.AddSeconds(60);
                        _logger.LogWarning("{Provider} chạm rate limit phút (429), tắt 60s.", provider);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi {Provider} model={Model}: {Message}", provider, config.Model, ex.Message);
                }
            }

            // OpenAI-compatible providers (Cerebras, SambaNova, Ollama local...) đọc từ "AiProviders"
            var openAiProviders = LoadOpenAiProviders();

            foreach (var p in openAiProviders)
            {
                if (string.IsNullOrEmpty(p.BaseUrl) || string.IsNullOrEmpty(p.Model)) continue;
                if (IsInCooldown(p.Name)) continue;

                // Ollama local (localhost) không bao giờ 429 → fallback vô hạn, không tính vào "exhausted"
                var isLocal = p.BaseUrl.Contains("localhost") || p.BaseUrl.Contains("127.0.0.1");

                try
                {
                    var result = await InvokeOpenAICompatibleAsync(p.BaseUrl, p.ApiKey, p.Model, @abstract, paperTitle);
                    anyProviderResponded = true;
                    if (result.Count > 0) return result;
                }
                catch (Exception ex) when (!isLocal && (ex.Message.Contains("429") || ex.Message.Contains("rate")
                    || ex.Message.Contains("quota") || ex.Message.Contains("RESOURCE_EXHAUSTED")))
                {
                    // Mặc định coi 429 là giới hạn NGÀY chỉ khi chắc chắn; còn lại nghỉ ngắn 60s
                    var isDailyLimit = ex.Message.Contains("per day") || ex.Message.Contains("RPD")
                        || ex.Message.Contains("daily") || ex.Message.Contains("TPD");
                    if (isDailyLimit)
                    {
                        TripBreakerUntilNextUtcMidnight(p.Name);
                        _logger.LogWarning("{Provider} hết quota NGÀY (429), tắt đến 00:00 UTC.", p.Name);
                    }
                    else
                    {
                        _cooldownUntilUtc[p.Name] = DateTime.UtcNow.AddSeconds(60);
                        _logger.LogWarning("{Provider} chạm rate limit phút (429), tắt 60s.", p.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi {Provider} model={Model}: {Message}", p.Name, p.Model, ex.Message);
                }
            }

            // Không provider nào phản hồi → tất cả đang cooldown/429 → báo để bulk processor DỪNG
            if (!anyProviderResponded)
                throw new AllProvidersExhaustedException(
                    $"Mọi AI provider đều hết quota/đang cooldown khi xử lý '{paperTitle}'.");

            // Có provider phản hồi nhưng trả 0 keyword → thật sự không extract được
            _logger.LogWarning("AI phản hồi nhưng 0 keyword cho paper '{Title}'.", paperTitle);
            return new List<string>();
        }

        /// <summary>
        /// Đọc danh sách Gemini key từ config. Ưu tiên mảng Gemini:Keys[],
        /// fallback về format cũ ApiKey1/ApiKey2 nếu mảng rỗng (giữ tương thích ngược).
        /// </summary>
        private List<(string ApiKey, string Model)> LoadGeminiKeys()
        {
            var keys = new List<(string ApiKey, string Model)>();

            // Format mới: mảng "Gemini:Keys": [ { "ApiKey": "...", "Model": "..." } ]
            var section = _configuration.GetSection("Gemini:Keys");
            foreach (var child in section.GetChildren())
            {
                var apiKey = child["ApiKey"];
                if (string.IsNullOrEmpty(apiKey)) continue;
                keys.Add((apiKey, child["Model"] ?? "gemini-2.0-flash"));
            }

            if (keys.Count > 0) return keys;

            // Format cũ: ApiKey1/Model1, ApiKey2/Model2
            var key1 = _configuration["Gemini:ApiKey1"];
            if (!string.IsNullOrEmpty(key1))
                keys.Add((key1, _configuration["Gemini:Model1"] ?? "gemini-2.0-flash"));

            var key2 = _configuration["Gemini:ApiKey2"];
            if (!string.IsNullOrEmpty(key2))
                keys.Add((key2, _configuration["Gemini:Model2"] ?? "gemini-2.0-flash-lite"));

            return keys;
        }

        /// <summary>
        /// Đọc danh sách Groq key từ config. Ưu tiên mảng Groq:Keys[],
        /// fallback về Groq:ApiKey đơn (giữ tương thích ngược). Nhiều key = nhân quota ngày.
        /// </summary>
        private List<(string ApiKey, string Model)> LoadGroqKeys()
        {
            var defaultModel = _configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";
            var keys = new List<(string ApiKey, string Model)>();

            var section = _configuration.GetSection("Groq:Keys");
            foreach (var child in section.GetChildren())
            {
                var apiKey = child["ApiKey"];
                if (string.IsNullOrEmpty(apiKey)) continue;
                keys.Add((apiKey, child["Model"] ?? defaultModel));
            }

            if (keys.Count > 0) return keys;

            // Fallback: format cũ Groq:ApiKey đơn
            var single = _configuration["Groq:ApiKey"];
            if (!string.IsNullOrEmpty(single))
                keys.Add((single, defaultModel));

            return keys;
        }

        /// <summary>
        /// Đọc danh sách provider OpenAI-compatible từ section "AiProviders".
        /// Mỗi item: { Name, BaseUrl, ApiKey, Model }. Dùng chung cho Cerebras/SambaNova/Ollama.
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

        private async Task<List<string>> InvokeOpenAICompatibleAsync(string baseUrl, string apiKey, string model, string @abstract, string paperTitle)
        {
            var kernel = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(
                    modelId: model,
                    apiKey: apiKey,
                    httpClient: new HttpClient { BaseAddress = new Uri(baseUrl) })
                .Build();

            var function = kernel.CreateFunctionFromPrompt(PromptTemplate);

            var result = await kernel.InvokeAsync(function, new KernelArguments
            {
                ["title"] = paperTitle,
                ["abstract"] = @abstract
            });

            return ParseKeywords(result.ToString().Trim(), paperTitle);
        }

        public IReadOnlyDictionary<string, DateTime> GetProviderCooldowns()
        {
            // Chỉ trả về provider còn đang trong cooldown (chưa tới giờ bật lại)
            var now = DateTime.UtcNow;
            return _cooldownUntilUtc
                .Where(kv => kv.Value > now)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        private bool IsInCooldown(string provider)
        {
            return _cooldownUntilUtc.TryGetValue(provider, out var until) && DateTime.UtcNow < until;
        }

        private void TripBreakerUntilNextUtcMidnight(string provider)
        {
            _cooldownUntilUtc[provider] = DateTime.UtcNow.Date.AddDays(1);
        }

        private async Task<List<string>> InvokeGeminiAsync(string apiKey, string model, string @abstract, string paperTitle)
        {
            var kernel = Kernel.CreateBuilder()
                .AddGoogleAIGeminiChatCompletion(model, apiKey)
                .Build();

            var function = kernel.CreateFunctionFromPrompt(PromptTemplate);

            var result = await kernel.InvokeAsync(function, new KernelArguments
            {
                ["title"] = paperTitle,
                ["abstract"] = @abstract
            });

            var rawResponse = result.ToString().Trim();
            return ParseKeywords(rawResponse, paperTitle);
        }

        private async Task<List<string>> InvokeGroqAsync(string apiKey, string model, string @abstract, string paperTitle)
        {
            var kernel = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(
                    modelId: model,
                    apiKey: apiKey,
                    httpClient: new HttpClient { BaseAddress = new Uri("https://api.groq.com/openai/v1/") })
                .Build();

            var function = kernel.CreateFunctionFromPrompt(PromptTemplate);

            var result = await kernel.InvokeAsync(function, new KernelArguments
            {
                ["title"] = paperTitle,
                ["abstract"] = @abstract
            });

            return ParseKeywords(result.ToString().Trim(), paperTitle);
        }

        private List<string> ParseKeywords(string rawResponse, string paperTitle)
        {
            try
            {
                // Tách phần JSON array ra khỏi response (Gemini đôi khi thêm text thừa)
                var start = rawResponse.IndexOf('[');
                var end = rawResponse.LastIndexOf(']');

                if (start == -1 || end == -1 || end <= start)
                {
                    _logger.LogWarning("Gemini trả về format không hợp lệ cho '{Title}': {Response}", paperTitle, rawResponse);
                    return new List<string>();
                }

                var jsonArray = rawResponse.Substring(start, end - start + 1);
                var keywords = System.Text.Json.JsonSerializer.Deserialize<List<string>>(jsonArray);

                return keywords?
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Select(k => k.ToLower().Trim())
                    .Distinct()
                    .Take(8)
                    .ToList() ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Parse keyword thất bại cho paper '{Title}'. Raw: {Response}", paperTitle, rawResponse);
                return new List<string>();
            }
        }
    }
}

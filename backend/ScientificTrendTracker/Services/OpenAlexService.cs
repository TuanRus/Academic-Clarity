using System.Text.Json;
using System.Text.Json.Serialization;
using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    public class OpenAlexService : IOpenAlexService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenAlexService> _logger;
        private readonly IConfiguration _configuration;

        // Computer Science FIELD trong taxonomy Topics của OpenAlex (fields/17).
        // Lọc theo primary_topic.field (chủ đề CHÍNH) thay vì concepts (tag phụ) → bài đúng ngành CS,
        // tránh lọt bài chỉ "dính" CS ở mức phụ (lạc scope).
        private const string CsFieldId = "fields/17";

        public OpenAlexService(HttpClient httpClient, ILogger<OpenAlexService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Gọi OpenAlex REST API lấy danh sách bài báo Computer Science theo trang.
        /// Quality gate cố định: CS concept + type article/proceedings-article + có abstract.
        /// Tham số điều chỉnh khoảng năm, ngưỡng citation và cách sắp xếp (top-cited hay mới nhất).
        /// </summary>
        /// <param name="page">int - Caller truyền vào - Số trang cần fetch, bắt đầu từ 1.</param>
        /// <param name="pageSize">int - Caller truyền vào - Số bài mỗi trang, mặc định 200 (trần OpenAlex).</param>
        /// <param name="fromYear">int - Caller truyền vào - Năm bắt đầu của khoảng lọc, mặc định 2022.</param>
        /// <param name="minCitedExclusive">
        /// int - Caller truyền vào - N trong "cited_by_count:>N". &lt;0 = bỏ lọc citation (gồm cả bài chưa được trích dẫn).
        /// Mặc định 2 (tức &gt;=3 citation).
        /// </param>
        /// <param name="recentFirst">
        /// bool - Caller truyền vào - true = sort theo ngày xuất bản (bài mới nổi); false = sort theo citation (top-cited).
        /// </param>
        /// <param name="toYear">int - Caller truyền vào - Năm kết thúc của khoảng lọc, mặc định 2026.</param>
        /// <returns>
        /// List&lt;OpenAlexPaper&gt; - Danh sách bài đã map từ JSON OpenAlex, mỗi bài gồm:
        /// - Id (string): OpenAlex work ID "https://openalex.org/W..."
        /// - Doi (string): DOI, có thể null
        /// - Title (string): Tiêu đề
        /// - PublicationYear (int?), PublicationDate (string): Năm/ngày xuất bản
        /// - CitedByCount (int): Số lượt trích dẫn
        /// - AbstractReconstructed (string): Abstract đã reconstruct — KHÔNG lưu DB
        /// - PrimaryLocation.Source.IssnL (string): ISSN để tra Q-rank SCImago
        /// - Authorships (List): Tác giả kèm thứ tự và affiliation
        /// Trả về list rỗng nếu hết trang hoặc API lỗi.
        /// </returns>
        public async Task<List<OpenAlexPaper>> FetchPapersAsync(
            int page, int pageSize = 200, int fromYear = 2022, int minCitedExclusive = 2,
            bool recentFirst = false, int toYear = 0)
        {
            if (toYear <= 0) toYear = DateTime.UtcNow.Year; // mặc định = năm hiện tại (không hardcode)
            var email = _configuration["OpenAlex:Email"];
            var baseUrl = _configuration["OpenAlex:BaseUrl"];

            // recentFirst=true: sort theo ngày (vét bài mới nổi 2025-2026); ngược lại sort theo citation (top-cited).
            var sort = recentFirst ? "publication_date:desc" : "cited_by_count:desc";

            // minCitedExclusive là N trong "cited_by_count:>N". <0 = bỏ lọc citation (gồm cả bài chưa được trích dẫn,
            // cần cho bài 2026 mới ra). Mặc định 2 = ">2" (>=3 citation) như filter top-cited gốc.
            var citationFilter = minCitedExclusive >= 0 ? $",cited_by_count:>{minCitedExclusive}" : "";

            // Quality gate: chỉ bài báo + kỷ yếu hội nghị (CS coi trọng NeurIPS/CVPR...), BẮT BUỘC có abstract
            // (cần để trích keyword), và CHỈ tiếng Anh (language:en) → lọc dataset/erratum/preprint/bài ngoại ngữ.
            const string qualityFilter = ",type:article|proceedings-article,has_abstract:true,language:en";

            var url = $"{baseUrl}/works" +
                      $"?filter=primary_topic.field.id:{CsFieldId},publication_year:{fromYear}-{toYear}{qualityFilter}{citationFilter}" +
                      $"&select=id,doi,title,publication_year,publication_date,cited_by_count,abstract_inverted_index,primary_location,authorships,primary_topic,keywords" +
                      $"&sort={sort}" +
                      $"&page={page}&per-page={pageSize}" +
                      $"&mailto={email}";

            _logger.LogInformation("Fetching OpenAlex page {Page}, pageSize {PageSize}", page, pageSize);

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAlex trả về {StatusCode} tại page {Page}", response.StatusCode, page);

                // 429 = hết budget/rate-limit: KHÔNG nuốt im lặng (trước đây trả list rỗng khiến sync báo
                // "hết data" total=0). Throw để orchestrator ghi ErrorMessage và đánh dấu sync FAILED rõ ràng.
                if ((int)response.StatusCode == 429)
                    throw new HttpRequestException(
                        "OpenAlex rate limit / hết budget ngày (HTTP 429). Đặt OpenAlex:Email hợp lệ để vào polite pool, " +
                        "đợi budget reset lúc nửa đêm UTC, hoặc nạp credit tại openalex.org/pricing.");

                throw new HttpRequestException($"OpenAlex trả về HTTP {(int)response.StatusCode} tại page {page}.");
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OpenAlexResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result?.Results == null || result.Results.Count == 0)
            {
                _logger.LogInformation("OpenAlex không còn data tại page {Page}", page);
                return new List<OpenAlexPaper>();
            }

            var papers = new List<OpenAlexPaper>();

            foreach (var raw in result.Results)
            {
                var abstractText = raw.AbstractInvertedIndex != null
                    ? ReconstructAbstract(raw.AbstractInvertedIndex)
                    : null;

                var paper = new OpenAlexPaper
                {
                    Id = raw.Id,
                    Doi = raw.Doi,
                    Title = raw.Title,
                    PublicationYear = raw.PublicationYear,
                    PublicationDate = raw.PublicationDate,
                    CitedByCount = raw.CitedByCount,
                    AbstractReconstructed = abstractText,
                    PrimaryLocation = MapLocation(raw.PrimaryLocation),
                    Authorships = MapAuthorships(raw.Authorships),
                    Topic = raw.PrimaryTopic?.DisplayName,
                    Keywords = MapKeywords(raw.Keywords)
                };

                papers.Add(paper);
            }

            _logger.LogInformation("Fetch xong page {Page}: {Count} papers", page, papers.Count);
            return papers;
        }

        /// <summary>
        /// Gọi OpenAlex lấy RIÊNG abstract của 1 bài theo OpenAlexId (dùng khi reprocess keyword cho bài đã lưu).
        /// Abstract chỉ tồn tại trong memory để trích keyword, tuyệt đối KHÔNG lưu vào DB.
        /// </summary>
        /// <param name="openAlexId">
        /// string - Caller truyền vào (lấy từ ResearchPaper.OpenAlexId) - Mã work OpenAlex dạng "W...".
        /// </param>
        /// <returns>
        /// string - Full text abstract đã ghép từ inverted index. Null nếu bài không có abstract hoặc API lỗi.
        /// </returns>
        public async Task<string> FetchAbstractByIdAsync(string openAlexId)
        {
            var email = _configuration["OpenAlex:Email"];
            var baseUrl = _configuration["OpenAlex:BaseUrl"];
            var url = $"{baseUrl}/works/{openAlexId}?select=abstract_inverted_index&mailto={email}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("abstract_inverted_index", out var indexEl)
                    || indexEl.ValueKind == JsonValueKind.Null)
                    return null;

                var invertedIndex = JsonSerializer.Deserialize<Dictionary<string, List<int>>>(
                    indexEl.GetRawText());

                return ReconstructAbstract(invertedIndex);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không fetch được abstract cho {OpenAlexId}", openAlexId);
                return null;
            }
        }

        /// <summary>Lấy NHANH chỉ primary_topic.display_name của 1 bài (request nhẹ). Trả null nếu không có.</summary>
        public async Task<string> FetchTopicByIdAsync(string openAlexId)
        {
            var email = _configuration["OpenAlex:Email"];
            var baseUrl = _configuration["OpenAlex:BaseUrl"];
            var id = openAlexId?.Replace("https://openalex.org/", "").Trim();
            var url = $"{baseUrl}/works/{id}?select=primary_topic&mailto={email}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("primary_topic", out var pt)
                    && pt.ValueKind == JsonValueKind.Object
                    && pt.TryGetProperty("display_name", out var dn))
                    return dn.GetString();
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không fetch được topic cho {OpenAlexId}", openAlexId);
                return null;
            }
        }

        /// <summary>
        /// Lấy chi tiết bổ sung của 1 bài (abstract + primary_topic + open_access + institutions) trong 1 request.
        /// Dùng cho màn Paper Detail của FE. Trả null nếu API lỗi.
        /// </summary>
        public async Task<OpenAlexWorkDetail> FetchWorkDetailAsync(string openAlexId)
        {
            var email = _configuration["OpenAlex:Email"];
            var baseUrl = _configuration["OpenAlex:BaseUrl"];
            // Chuẩn hoá: chấp nhận cả "W..." lẫn URL đầy đủ.
            var id = openAlexId?.Replace("https://openalex.org/", "").Trim();
            var url = $"{baseUrl}/works/{id}" +
                      $"?select=abstract_inverted_index,primary_topic,open_access,authorships&mailto={email}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var raw = JsonSerializer.Deserialize<OpenAlexRawWork>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (raw == null) return null;

                // Institution: gộp distinct theo thứ tự xuất hiện, bỏ rỗng.
                var institutions = (raw.Authorships ?? new List<OpenAlexRawAuthorship>())
                    .SelectMany(a => a.Institutions ?? new List<OpenAlexRawInstitution>())
                    .Select(i => i.DisplayName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToList();

                return new OpenAlexWorkDetail
                {
                    Abstract = raw.AbstractInvertedIndex != null ? ReconstructAbstract(raw.AbstractInvertedIndex) : null,
                    Topic = raw.PrimaryTopic?.DisplayName,
                    Subfield = raw.PrimaryTopic?.Subfield?.DisplayName,
                    Field = raw.PrimaryTopic?.Field?.DisplayName,
                    Domain = raw.PrimaryTopic?.Domain?.DisplayName,
                    OpenAccessStatus = raw.OpenAccess?.OaStatus,
                    Institutions = institutions
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không fetch được chi tiết OpenAlex cho {OpenAlexId}", openAlexId);
                return null;
            }
        }

        /// <summary>
        /// Lấy toàn bộ metadata 1 bài từ OpenAlex cho luồng Admin thêm thủ công bằng link/DOI/ID.
        /// </summary>
        public async Task<OpenAlexPaper> FetchSingleWorkAsync(string idOrDoiOrUrl)
        {
            if (string.IsNullOrWhiteSpace(idOrDoiOrUrl)) return null;

            var identifier = NormalizeWorkIdentifier(idOrDoiOrUrl.Trim());
            if (identifier == null)
            {
                _logger.LogWarning("Không nhận dạng được định danh OpenAlex/DOI từ '{Input}'", idOrDoiOrUrl);
                return null;
            }

            var email = _configuration["OpenAlex:Email"];
            var baseUrl = _configuration["OpenAlex:BaseUrl"];
            var url = $"{baseUrl}/works/{identifier}" +
                      "?select=id,doi,title,publication_year,publication_date,cited_by_count," +
                      "abstract_inverted_index,primary_location,authorships,primary_topic,keywords" +
                      $"&mailto={email}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OpenAlex trả về {StatusCode} khi fetch work '{Id}'", response.StatusCode, identifier);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var raw = JsonSerializer.Deserialize<OpenAlexRawWork>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (raw == null || string.IsNullOrWhiteSpace(raw.Title)) return null;

                return new OpenAlexPaper
                {
                    Id = raw.Id,
                    Doi = raw.Doi,
                    Title = raw.Title,
                    PublicationYear = raw.PublicationYear,
                    PublicationDate = raw.PublicationDate,
                    CitedByCount = raw.CitedByCount,
                    AbstractReconstructed = raw.AbstractInvertedIndex != null ? ReconstructAbstract(raw.AbstractInvertedIndex) : null,
                    PrimaryLocation = MapLocation(raw.PrimaryLocation),
                    Authorships = MapAuthorships(raw.Authorships),
                    Topic = raw.PrimaryTopic?.DisplayName,
                    Keywords = MapKeywords(raw.Keywords)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không fetch được work OpenAlex từ '{Input}'", idOrDoiOrUrl);
                return null;
            }
        }

        /// <summary>
        /// Chuẩn hoá đầu vào của Admin (link OpenAlex / link DOI / ID / DOI trần) về path-segment mà
        /// endpoint /works chấp nhận. Trả null nếu không nhận dạng được.
        /// </summary>
        private static string NormalizeWorkIdentifier(string input)
        {
            // 1) URL OpenAlex: chấp nhận cả https://openalex.org/W123 lẫn .../works/W123
            var oaIdx = input.IndexOf("openalex.org/", StringComparison.OrdinalIgnoreCase);
            if (oaIdx >= 0)
            {
                var id = input[(oaIdx + "openalex.org/".Length)..].Trim().Trim('/');
                // Bỏ tiền tố "works/" nếu có (link dạng openalex.org/works/W123).
                if (id.StartsWith("works/", StringComparison.OrdinalIgnoreCase))
                    id = id["works/".Length..].Trim('/');
                // Bỏ query string nếu có.
                var q = id.IndexOf('?');
                if (q >= 0) id = id[..q];
                if (string.IsNullOrWhiteSpace(id)) return null;
                // Chuẩn hoá "w123" → "W123" (OpenAlex yêu cầu W hoa).
                if ((id[0] == 'w' || id[0] == 'W') && id.Length > 1 && id[1..].All(char.IsDigit))
                    return "W" + id[1..];
                return id;
            }

            // 2) ID trần dạng "W" + chữ số (không phân biệt hoa/thường)
            if (input.Length > 1
                && (input[0] == 'W' || input[0] == 'w')
                && input.Skip(1).All(char.IsDigit))
                return "W" + input[1..];

            // 3) DOI: chấp nhận cả URL doi.org lẫn DOI trần "10.xxxx/..."
            var doiIdx = input.IndexOf("doi.org/", StringComparison.OrdinalIgnoreCase);
            var doi = doiIdx >= 0 ? input[(doiIdx + "doi.org/".Length)..].Trim() : input;
            if (doi.StartsWith("10.")) return "doi:" + doi;

            return null;
        }

        /// <summary>
        /// Reconstruct full text abstract từ abstract_inverted_index của OpenAlex.
        /// OpenAlex lưu dạng inverted index (thay full text) để tránh vấn đề bản quyền.
        /// </summary>
        /// <param name="invertedIndex">
        /// Dictionary&lt;string, List&lt;int&gt;&gt; - OpenAlex trả về ở field "abstract_inverted_index" -
        /// Key là từ, Value là danh sách vị trí của từ đó trong câu.
        /// </param>
        /// <returns>
        /// string - Full text abstract đã ghép theo thứ tự vị trí. Null nếu invertedIndex null/rỗng.
        /// </returns>
        public string ReconstructAbstract(Dictionary<string, List<int>> invertedIndex)
        {
            if (invertedIndex == null || invertedIndex.Count == 0)
                return null;

            var maxPosition = invertedIndex.Values
                .SelectMany(positions => positions)
                .Max();

            var words = new string[maxPosition + 1];

            foreach (var (word, positions) in invertedIndex)
                foreach (var pos in positions)
                    if (pos >= 0 && pos <= maxPosition)
                        words[pos] = word;

            return string.Join(" ", words.Where(w => w != null));
        }

        /// <summary>Map location thô (JSON OpenAlex) sang OpenAlexLocation gọn, chỉ giữ thông tin Source (tạp chí).</summary>
        /// <param name="raw">OpenAlexRawLocation - Deserialize từ JSON - Location thô, có thể null.</param>
        /// <returns>OpenAlexLocation - Null nếu raw hoặc Source null.</returns>
        private OpenAlexLocation MapLocation(OpenAlexRawLocation raw)
        {
            if (raw?.Source == null) return null;
            return new OpenAlexLocation
            {
                Source = new OpenAlexSource
                {
                    Id = raw.Source.Id,
                    DisplayName = raw.Source.DisplayName,
                    IssnL = raw.Source.IssnL,
                    Issn = raw.Source.Issn ?? new List<string>()
                }
            };
        }

        /// <summary>
        /// Map keyword OpenAlex: lấy slug đã chuẩn hoá từ id ("keywords/&lt;slug&gt;"), lọc theo score,
        /// bỏ slug quá ngắn/generic, giữ tối đa 8. Đây là nguồn keyword chất lượng, không cần AI.
        /// </summary>
        private static List<string> MapKeywords(List<OpenAlexRawKeyword> raw)
        {
            if (raw == null) return new List<string>();
            const double minScore = 0.45; // dưới ngưỡng này keyword thường nhiễu/quá chung
            return raw
                .Where(k => k.Score >= minScore && !string.IsNullOrWhiteSpace(k.Id))
                .Select(k =>
                {
                    var idx = k.Id.LastIndexOf('/');
                    var slug = idx >= 0 ? k.Id[(idx + 1)..] : k.DisplayName;
                    return KeywordNormalizer.Normalize(slug);
                })
                .Where(s => s.Length >= 3 && !KeywordStopwords.IsGeneric(s)) // bỏ generic/tên ngành (nhiễu mindmap)
                .Distinct()
                .Take(8)
                .ToList();
        }

        /// <summary>Map danh sách authorship thô (JSON OpenAlex) sang OpenAlexAuthorship gọn (tác giả + affiliation).</summary>
        /// <param name="rawList">List&lt;OpenAlexRawAuthorship&gt; - Deserialize từ JSON - Danh sách thô, có thể null.</param>
        /// <returns>List&lt;OpenAlexAuthorship&gt; - List rỗng nếu rawList null.</returns>
        private List<OpenAlexAuthorship> MapAuthorships(List<OpenAlexRawAuthorship> rawList)
        {
            if (rawList == null) return new List<OpenAlexAuthorship>();

            return rawList.Select((raw, index) => new OpenAlexAuthorship
            {
                AuthorPosition = index + 1,
                Author = raw.Author == null ? null : new OpenAlexAuthor
                {
                    Id = raw.Author.Id,
                    DisplayName = raw.Author.DisplayName
                },
                Institutions = (raw.Institutions ?? new List<OpenAlexRawInstitution>())
                    .Select(i => new OpenAlexInstitution { DisplayName = i.DisplayName })
                    .ToList()
            }).ToList();
        }

        // Raw JSON models đã tách sang OpenAlexRawModels.cs (cùng namespace).
    }
}

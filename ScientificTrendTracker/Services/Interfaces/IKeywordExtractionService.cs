namespace ScientificTrendTracker.Services.Interfaces
{
    public interface IKeywordExtractionService
    {
        /// <summary>
        /// Gửi abstract bài báo lên Gemini AI để trích xuất keyword kỹ thuật đặc trưng.
        /// Tự động fallback sang ApiKey2 nếu ApiKey1 bị rate limit (HTTP 429).
        /// Abstract chỉ tồn tại trong memory, KHÔNG được lưu vào DB.
        /// </summary>
        /// <param name="abstract">
        /// string - IOpenAlexService.ReconstructAbstract() trả về -
        /// Full text abstract đã ghép lại từ inverted index. Truyền null hoặc rỗng sẽ trả về list rỗng ngay.
        /// </param>
        /// <param name="paperTitle">
        /// string - OpenAlexPaper.Title lấy từ OpenAlex API -
        /// Tiêu đề bài báo, bổ sung context giúp Gemini extract keyword chính xác hơn.
        /// </param>
        /// <returns>
        /// List&lt;string&gt; - Danh sách keyword kỹ thuật do Gemini trích xuất.
        /// Mỗi keyword là:
        /// - (string): Lowercase, dùng hyphen cho cụm từ (vd: "machine-learning", "large-language-model")
        /// - Tối đa 8 keyword mỗi bài
        /// - Chỉ chứa thuật ngữ kỹ thuật đặc trưng (loại bỏ từ generic như "study", "result")
        /// Trả về list rỗng nếu abstract null, Gemini lỗi, hoặc tất cả API key đều thất bại.
        /// </returns>
        Task<List<string>> ExtractKeywordsAsync(string @abstract, string paperTitle);

        /// <summary>
        /// Trả về trạng thái circuit-breaker hiện tại của từng AI provider.
        /// Dùng để debug: provider nào đang bị tắt do hết quota/rate limit và đến khi nào.
        /// </summary>
        /// <returns>
        /// Dictionary&lt;string, DateTime&gt; - Key là tên provider (gemini-key1, groq...),
        /// Value là thời điểm UTC provider đó được bật lại. Rỗng nếu không có provider nào đang cooldown.
        /// </returns>
        IReadOnlyDictionary<string, DateTime> GetProviderCooldowns();
    }

    /// <summary>
    /// Ném ra khi MỌI AI provider đều không phản hồi được (đang cooldown hoặc trả 429).
    /// Khác với việc provider có phản hồi nhưng trả về 0 keyword (lúc đó trả list rỗng).
    /// Bulk processor bắt exception này để DỪNG lại (chờ quota reset) thay vì đánh dấu nhầm bài là đã xử lý.
    /// </summary>
    public class AllProvidersExhaustedException : Exception
    {
        public AllProvidersExhaustedException(string message) : base(message) { }
    }
}

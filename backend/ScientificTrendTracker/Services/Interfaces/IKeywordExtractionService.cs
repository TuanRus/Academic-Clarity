namespace ScientificTrendTracker.Services.Interfaces
{
    public interface IKeywordExtractionService
    {
        /// <summary>
        /// Trích keyword kỹ thuật từ abstract bằng AI local (Ollama, OpenAI-compatible).
        /// Abstract chỉ tồn tại trong memory, KHÔNG được lưu vào DB.
        /// </summary>
        /// <param name="abstract">
        /// string - IOpenAlexService.ReconstructAbstract() trả về -
        /// Full text abstract đã ghép lại từ inverted index. Truyền null hoặc rỗng sẽ trả về list rỗng ngay.
        /// </param>
        /// <param name="paperTitle">
        /// string - OpenAlexPaper.Title - Tiêu đề bài báo, bổ sung context giúp AI trích chính xác hơn.
        /// </param>
        /// <param name="seedKeywords">
        /// IReadOnlyList&lt;string&gt; (tùy chọn) - Keyword OpenAlex sẵn có của bài (controlled vocabulary).
        /// Khi có, được nhúng vào prompt làm "mẫu" để AI bám phong cách OpenAlex: ưu tiên tái dùng/chuẩn hoá
        /// các term này, chỉ bổ sung thêm term kỹ thuật mới cùng style. Null/rỗng = trích thuần như cũ.
        /// </param>
        /// <returns>
        /// List&lt;string&gt; - Keyword (lowercase + dấu cách, tối đa 8). List rỗng nếu abstract null hoặc AI lỗi.
        /// </returns>
        Task<List<string>> ExtractKeywordsAsync(string @abstract, string paperTitle, IReadOnlyList<string> seedKeywords = null);

        /// <summary>
        /// Sinh văn bản tự do từ 1 prompt bằng AI local (Ollama) — dùng làm FALLBACK cho phân tích trùng ý tưởng
        /// khi Gemini không khả dụng. Trả text thô của model, hoặc null nếu không provider nào chạy được.
        /// </summary>
        Task<string> CompleteAsync(string prompt, CancellationToken ct = default);
    }
}

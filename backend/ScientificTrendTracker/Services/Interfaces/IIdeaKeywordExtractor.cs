namespace ScientificTrendTracker.Services.Interfaces
{
    /// <summary>
    /// Trích keyword cho Idea Check: ưu tiên Google Gemini với 2 API key luân phiên + failover;
    /// nếu CẢ 2 key đều bị rate-limit (429)/lỗi thì fallback sang AI local (Ollama).
    /// Abstract chỉ xử lý in-memory, KHÔNG lưu DB.
    /// </summary>
    public interface IIdeaKeywordExtractor
    {
        /// <param name="abstractText">Đoạn abstract người dùng dán (in-memory).</param>
        /// <param name="ct">CancellationToken để áp timeout cho lời gọi Gemini.</param>
        /// <returns>Danh sách keyword đã chuẩn hoá (tối đa 8); rỗng nếu mọi provider thất bại.</returns>
        Task<List<string>> ExtractKeywordsAsync(string abstractText, CancellationToken ct);

        /// <summary>
        /// Gọi AI (Gemini, 2 key luân phiên) sinh văn bản cho 1 prompt tự do — dùng để phân tích trùng Ý TƯỞNG.
        /// Trả về text thô của model, hoặc null nếu không cấu hình key / mọi key thất bại (degrade êm, không fallback).
        /// </summary>
        Task<string> AnalyzeAsync(string prompt, CancellationToken ct);
    }
}

namespace ScientificTrendTracker.Services.Interfaces
{
    public interface ISyncOrchestratorService
    {
        /// <summary>
        /// Fetch OpenAlex → extract keywords → lưu DB.
        /// Fetch tối đa maxPages trang, mỗi trang 200 bài, bỏ qua bài đã có.
        /// </summary>
        /// <param name="maxPages">
        /// int - Caller truyền vào - Số trang tối đa cần fetch. Weekly sync dùng 50, trigger thủ công dùng ít hơn.
        /// </param>
        /// <param name="cancellationToken">
        /// CancellationToken - Caller truyền vào - Dừng gracefully khi cần.
        /// </param>
        /// <param name="skipKeywords">
        /// bool - Caller truyền vào - true = chỉ fetch + lưu paper, KHÔNG gọi AI extract keyword
        /// (IsAiProcessed=false, để reprocess-all xử lý keyword sau). Nạp nhanh, không delay.
        /// false = gọi AI inline như cũ. Mặc định false.
        /// </param>
        /// <returns>
        /// SyncResult - Breakdown chi tiết số bài theo từng kết quả xử lý.
        /// </returns>
        Task<SyncResult> RunSyncAsync(int maxPages, bool skipKeywords = false,
            int fromYear = 2022, int minCitedExclusive = 2, bool recentFirst = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Backfill CÂN BẰNG theo từng năm: lặp mỗi năm từ fromYear→toYear, lấy tối đa perYearCap bài/năm.
        /// Năm cũ (≤2024) sort theo citation (top notable); năm gần (≥2025) sort theo ngày + bỏ lọc citation
        /// (bài mới chưa kịp được trích dẫn). Mục tiêu: mỗi năm có mẫu đủ + cùng tiêu chí → trend share/năm fair.
        /// Cộng dồn, dedup theo OpenAlexId (chạy lại không trùng).
        /// </summary>
        /// <param name="perYearCap">Số bài tối đa mỗi năm (mặc định 2500). Trần OpenAlex paging là 10,000/năm.</param>
        /// <param name="fromYear">Năm bắt đầu (mặc định 2022).</param>
        /// <param name="toYear">Năm kết thúc (mặc định 2026).</param>
        /// <param name="skipKeywords">true = chỉ fetch+lưu, để reprocess-all đào keyword sau (mặc định true).</param>
        Task<SyncResult> RunBalancedBackfillAsync(int perYearCap = 2500, int fromYear = 2022, int toYear = 2026,
            bool skipKeywords = true, CancellationToken cancellationToken = default);
    }

    public class SyncResult
    {
        public int Added { get; set; }
        public int AlreadyExists { get; set; }
        public int NoTitle { get; set; }
        public int Errors { get; set; }
        public int Skipped => AlreadyExists + NoTitle + Errors;
    }
}


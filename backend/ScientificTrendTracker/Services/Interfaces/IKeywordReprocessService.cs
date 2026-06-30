namespace ScientificTrendTracker.Services.Interfaces
{
    public interface IKeywordReprocessService
    {
        /// <summary>
        /// Bắt đầu reprocess keyword cho TẤT CẢ bài báo IsAiProcessed=false ở chế độ chạy nền.
        /// Idempotent: nếu đang có job chạy thì không khởi động job mới.
        /// </summary>
        /// <returns>
        /// bool - true nếu job vừa được khởi động, false nếu đã có job đang chạy.
        /// </returns>
        bool StartBackground();

        /// <summary>
        /// Lấy trạng thái job reprocess hiện tại để theo dõi tiến độ. Trả về bản copy (thread-safe).
        /// </summary>
        /// <returns>
        /// ReprocessJobState - Gồm: IsRunning (bool), TotalAtStart (int), Processed (int), Failed (int),
        /// Remaining (int), StartedAtUtc (DateTime?), FinishedAtUtc (DateTime?), LastError (string), StopReason (string).
        /// </returns>
        ReprocessJobState GetState();
    }

    public class ReprocessJobState
    {
        public bool IsRunning { get; set; }
        public int TotalAtStart { get; set; }
        public int Processed { get; set; }
        public int Failed { get; set; }
        public int Remaining { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? FinishedAtUtc { get; set; }
        public string LastError { get; set; }
        public string StopReason { get; set; }
    }
}

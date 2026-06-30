namespace ScientificTrendTracker.Services.Interfaces
{
    /// <summary>
    /// Job nền cào lại Topic (primary_topic) cho TOÀN BỘ bài báo từ OpenAlex (đào từ đầu).
    /// Tách biệt với job đào keyword.
    /// </summary>
    public interface ITopicBackfillService
    {
        /// <returns>true nếu job vừa khởi động, false nếu đã có job đang chạy.</returns>
        bool StartBackground();
        TopicBackfillState GetState();
    }

    public class TopicBackfillState
    {
        public bool IsRunning { get; set; }
        public int Total { get; set; }
        public int Processed { get; set; }
        public int Updated { get; set; }
        public int Failed { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? FinishedAtUtc { get; set; }
        public string LastError { get; set; }
    }
}

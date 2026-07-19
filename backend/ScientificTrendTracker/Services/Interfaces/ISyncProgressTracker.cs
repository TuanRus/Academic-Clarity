using ScientificTrendTracker.Services;

namespace ScientificTrendTracker.Services.Interfaces
{
    /// <summary>Theo dõi tiến độ sync realtime trong bộ nhớ (singleton) để FE poll.</summary>
    public interface ISyncProgressTracker
    {
        bool IsRunning { get; }

        /// <summary>
        /// Đánh dấu "đang chạy" NGAY khi nhận yêu cầu sync, trước khi task nền kịp tạo ApiSyncLog.
        /// Tránh FE poll trúng snapshot của lần sync trước (nháy FINISHED → RUNNING) và giúp guard
        /// chống bấm sync trùng hoạt động đúng. Task nền gọi Begin(syncLogId) sau để gán id.
        /// </summary>
        void BeginPending();

        void Begin(int syncLogId);
        void Push(string title, string status);
        void End();
        SyncProgressSnapshot Snapshot();
    }
}

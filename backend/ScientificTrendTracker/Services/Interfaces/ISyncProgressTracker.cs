using ScientificTrendTracker.Services;

namespace ScientificTrendTracker.Services.Interfaces
{
    /// <summary>Theo dõi tiến độ sync realtime trong bộ nhớ (singleton) để FE poll.</summary>
    public interface ISyncProgressTracker
    {
        bool IsRunning { get; }
        void Begin(int syncLogId);
        void Push(string title, string status);
        void End();
        SyncProgressSnapshot Snapshot();
    }
}

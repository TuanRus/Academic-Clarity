using ScientificTrendTracker.Services.Interfaces;

namespace ScientificTrendTracker.Services
{
    /// <summary>1 dòng tiến độ realtime: thời gian - tiêu đề bài - trạng thái.</summary>
    public class SyncProgressEntry
    {
        public DateTime Time { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Success | Exists | Error | NoTitle
    }

    public class SyncProgressSnapshot
    {
        public bool IsRunning { get; set; }
        public int? SyncLogId { get; set; }
        public int Added { get; set; }
        public int Exists { get; set; }
        public int Errors { get; set; }
        public int Total { get; set; }
        public List<SyncProgressEntry> Entries { get; set; } = new();
    }

    /// <summary>
    /// Lưu tiến độ sync ĐANG chạy trong bộ nhớ (singleton) để FE poll xem realtime.
    /// Giữ tối đa 300 dòng gần nhất, thread-safe.
    /// </summary>
    public class SyncProgressTracker : ISyncProgressTracker
    {
        private readonly object _lock = new();
        private readonly List<SyncProgressEntry> _entries = new();
        private readonly HashSet<string> _seenTitles = new(StringComparer.OrdinalIgnoreCase);
        private bool _running;
        private int? _syncLogId;
        private int _added, _exists, _errors, _total;

        public bool IsRunning { get { lock (_lock) { return _running; } } }

        public void Begin(int syncLogId)
        {
            lock (_lock)
            {
                _running = true;
                _syncLogId = syncLogId;
                _entries.Clear();
                _seenTitles.Clear();
                _added = _exists = _errors = _total = 0;
            }
        }

        public void Push(string title, string status)
        {
            lock (_lock)
            {
                if (!_running) return;
                _total++;
                if (status == "Exists") { _exists++; return; }
                if (status == "Error") { _errors++; return; }
                if (status != "Success") return;

                // Feed realtime CHỈ hiển thị bài MỚI thực sự được thêm vào DB (giống màn Detail lúc kết thúc).
                // Dedup theo tiêu đề: OpenAlex đôi khi trả cùng 1 bài trên 2 trang → tránh đếm/hiển thị trùng.
                var key = string.IsNullOrWhiteSpace(title) ? "(no title)" : title.Trim();
                if (!_seenTitles.Add(key)) return;

                _added++;
                _entries.Add(new SyncProgressEntry
                {
                    Time = DateTime.UtcNow,
                    Title = key,
                    Status = status
                });
                if (_entries.Count > 300) _entries.RemoveAt(0);
            }
        }

        public void End()
        {
            lock (_lock) { _running = false; }
        }

        public SyncProgressSnapshot Snapshot()
        {
            lock (_lock)
            {
                return new SyncProgressSnapshot
                {
                    IsRunning = _running,
                    SyncLogId = _syncLogId,
                    Added = _added,
                    Exists = _exists,
                    Errors = _errors,
                    Total = _total,
                    Entries = _entries.ToList(),
                };
            }
        }
    }
}

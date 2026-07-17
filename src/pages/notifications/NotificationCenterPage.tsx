import { useBookmark } from '../../hooks/useBookmark';

// LS-06/R-06 · Notification Center - FR-25, FR-26
// NEW_CITATION được sinh ra cho mỗi paper đang trong trạng thái FOLLOWED
// (tức là đã Bookmark - xem BR-32 trong PaperDetailPage / SavedLibraryPage).
const NotificationCenterPage = () => {
  const { bookmarkedPapers } = useBookmark();

  const citationNotifications = bookmarkedPapers.map((p) => ({
    id: `cite-${p.paperId}`,
    type: 'NEW_CITATION' as const,
    text: `Your bookmarked paper "${p.title}" was cited in a new publication.`,
    time: 'Vài phút trước',
  }));

  const systemNotifications = [
    {
      id: 'sync-job',
      type: 'SYNC' as const,
      text: 'Sync job completed – 1,240 new papers added to your tracked landscape.',
      time: '10 phút trước',
    },
  ];

  const all = [...systemNotifications, ...citationNotifications];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-gray-400">
            LS-06/R-06 · Notification Center
          </p>
          <h1 className="text-2xl font-bold text-gray-900">Notifications & System Logs</h1>
        </div>
        <button className="rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800">
          Mark All as Read
        </button>
      </div>

      <div className="space-y-3">
        {all.map((n) => (
          <div key={n.id} className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
            <span
              className={`mr-2 rounded-full px-2 py-0.5 text-xs ${
                n.type === 'NEW_CITATION'
                  ? 'bg-indigo-50 text-indigo-700'
                  : 'bg-gray-100 text-gray-600'
              }`}
            >
              {n.type === 'NEW_CITATION' ? 'New Citation' : 'Sync Job'}
            </span>
            <span className="text-sm text-gray-800">{n.text}</span>
            <p className="mt-1 text-xs text-gray-400">{n.time}</p>
          </div>
        ))}

        {all.length === 0 && (
          <p className="rounded-xl border border-dashed border-gray-300 bg-white p-10 text-center text-sm text-gray-500">
            You&apos;re caught up with all important institutional updates.
          </p>
        )}
      </div>
    </div>
  );
};

export default NotificationCenterPage;

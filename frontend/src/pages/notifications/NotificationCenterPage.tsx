import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  getMyNotifications,
  markRead,
  markAllRead,
  type NotificationItem,
} from '../../lib/api/notification';
import { formatVnTime } from '../../lib/datetime';

// LS-06/R-06 · Notification Center — ĐÃ NỐI BE: thông báo thật (Notifications).
// (Danh sách Following đã chuyển sang trang Saved Library.)
const NotificationCenterPage = () => {
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [loading, setLoading] = useState(true);

  const load = () => {
    setLoading(true);
    getMyNotifications(50)
      .then(setItems)
      .catch(() => setItems([]))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load();
  }, []);

  const onMarkAll = async () => {
    await markAllRead().catch(() => { });
    setItems((prev) => prev.map((n) => ({ ...n, isRead: true })));
  };

  const onItemClick = async (n: NotificationItem) => {
    if (!n.isRead) {
      await markRead(n.notificationId).catch(() => { });
      setItems((prev) => prev.map((x) => (x.notificationId === n.notificationId ? { ...x, isRead: true } : x)));
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-gray-400">Notification Center</p>
          <h1 className="text-2xl font-bold text-gray-900">Notifications</h1>
        </div>

        <button
          onClick={onMarkAll}
          className="self-start rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800 sm:self-auto"
        >
          Mark All as Read
        </button>
      </div>

      {loading && <p className="text-sm text-gray-400">Loading…</p>}

      <div className="space-y-3">
        {!loading &&
          items.map((n) => (
            <div
              key={n.notificationId}
              onClick={() => onItemClick(n)}
              className={`cursor-pointer rounded-xl border p-4 shadow-sm ${n.isRead ? 'border-gray-200 bg-white' : 'border-indigo-200 bg-indigo-50/40'
                }`}
            >
              <div className="flex items-center gap-2">
                {!n.isRead && <span className="h-2 w-2 rounded-full bg-indigo-600" />}
                <span className="text-sm font-semibold text-gray-900">{n.title}</span>
              </div>

              <p className="mt-1 text-sm text-gray-700">{n.message}</p>

              <div className="mt-1 flex items-center gap-3 text-xs text-gray-400">
                <span>{formatVnTime(n.createdAt)}</span>
                {n.relatedPaperId && (
                  <Link
                    to={`/papers/${encodeURIComponent(n.relatedPaperId)}`}
                    className="text-indigo-600 hover:underline"
                  >
                    View paper
                  </Link>
                )}
              </div>
            </div>
          ))}

        {!loading && items.length === 0 && (
          <p className="rounded-xl border border-dashed border-gray-300 bg-white p-10 text-center text-sm text-gray-500">
            You&apos;re all caught up. Follow topics or journals to get notified about new papers.
          </p>
        )}
      </div>
    </div>
  );
};

export default NotificationCenterPage;
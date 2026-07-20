import { useCallback, useEffect, useState } from 'react';
import { Link, NavLink, Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { getActivityLogs, type ActivityLog } from '../../lib/api/admin';
import { Role } from '../../types/auth';

const adminSections = [
  {
    title: 'OVERVIEW',
    items: [{ to: '/admin', label: 'Dashboard', icon: '▦', end: true }],
  },
  {
    title: 'CONTENT',
    items: [
      {
        to: '/admin/repository',
        label: 'Article Repository',
        icon: '▤',
        end: false,
      },
    ],
  },
  {
    title: 'USERS',
    items: [
      {
        to: '/admin/users',
        label: 'User Management',
        icon: '◉',
        end: false,
      },
      {
        to: '/admin/revenue',
        label: 'Subscription & Payment',
        icon: '₫',
        end: false,
      },
    ],
  },
  {
    title: 'SYSTEM',
    items: [
      {
        to: '/admin/pipelines',
        label: 'Data Pipeline',
        icon: '◈',
        end: false,
      },
      {
        to: '/admin/logs',
        label: 'Activity Logs',
        icon: '▣',
        end: false,
      },
    ],
  },
];

const activityDotClass: Record<ActivityLog['type'], string> = {
  ELEVATION: 'bg-blue-500',
  LEDGER: 'bg-emerald-500',
  AUTH_FAIL: 'bg-red-500',
  UPDATE: 'bg-amber-500',
};

const activityTypeLabel: Record<ActivityLog['type'], string> = {
  ELEVATION: 'ADMIN',
  LEDGER: 'PAYMENT',
  AUTH_FAIL: 'WARNING',
  UPDATE: 'UPDATE',
};

/**
 * ActivityLog hiện tại chưa trả logId ra ngoài frontend.
 * Vì vậy sử dụng thời gian, loại và nội dung để nhận biết log mới.
 */
const getActivityFingerprint = (log: ActivityLog) =>
  `${log.time}__${log.type}__${log.title}__${log.ref}`;

const AdminLayout = () => {
  const { user } = useAuth();

  const [showNotifications, setShowNotifications] = useState(false);
  const [recentLogs, setRecentLogs] = useState<ActivityLog[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [notificationsLoading, setNotificationsLoading] = useState(false);
  const [notificationsError, setNotificationsError] = useState(false);

  /*
   * Mỗi tài khoản Admin có một mốc đã đọc riêng trong localStorage.
   */
  const lastSeenStorageKey = `admin-last-seen-activity-log-${user?.id ?? 'unknown'
    }`;

  /**
   * Lấy Activity Logs mới nhất.
   *
   * markAsSeen = true:
   * Xem tất cả log hiện tại là đã đọc và xóa badge đỏ.
   */
  const loadActivityNotifications = useCallback(
    async (markAsSeen = false) => {
      setNotificationsLoading(true);

      try {
        const logs = await getActivityLogs();

        /*
         * Popup chỉ hiện 5 log mới nhất.
         * API vẫn trả tối đa 50 log để tính unreadCount.
         */
        setRecentLogs(logs.slice(0, 5));
        setNotificationsError(false);

        if (logs.length === 0) {
          setUnreadCount(0);
          return;
        }

        const newestFingerprint = getActivityFingerprint(logs[0]);
        const lastSeenFingerprint = localStorage.getItem(
          lastSeenStorageKey,
        );

        /*
         * Lần đầu mở AdminLayout:
         * Không coi toàn bộ log cũ là thông báo chưa đọc.
         */
        if (!lastSeenFingerprint) {
          localStorage.setItem(
            lastSeenStorageKey,
            newestFingerprint,
          );

          setUnreadCount(0);
          return;
        }

        /*
         * Khi Admin mở popup, lưu log mới nhất làm mốc đã đọc.
         */
        if (markAsSeen) {
          localStorage.setItem(
            lastSeenStorageKey,
            newestFingerprint,
          );

          setUnreadCount(0);
          return;
        }

        /*
         * API trả log mới nhất trước.
         *
         * Ví dụ:
         * logs[0] = log mới
         * logs[1] = log mới
         * logs[2] = log đã xem gần nhất
         *
         * Khi đó có 2 log chưa đọc.
         */
        const lastSeenIndex = logs.findIndex(
          (log) =>
            getActivityFingerprint(log) ===
            lastSeenFingerprint,
        );

        /*
         * Không tìm thấy mốc cũ có thể do đã có hơn 50 log mới.
         * Khi đó coi toàn bộ danh sách vừa tải là chưa đọc.
         */
        setUnreadCount(
          lastSeenIndex === -1
            ? logs.length
            : lastSeenIndex,
        );
      } catch (error) {
        console.error(
          'Không thể tải Activity Logs:',
          error,
        );

        setNotificationsError(true);
      } finally {
        setNotificationsLoading(false);
      }
    },
    [lastSeenStorageKey],
  );

  /**
   * Tải log khi vào trang, kiểm tra lại mỗi 10 giây
   * và kiểm tra ngay khi quay lại tab trình duyệt.
   */
  useEffect(() => {
    if (!user || user.role !== Role.ADMIN) {
      return undefined;
    }

    void loadActivityNotifications();

    const intervalId = window.setInterval(() => {
      void loadActivityNotifications();
    }, 10000);

    const handleWindowFocus = () => {
      void loadActivityNotifications();
    };

    window.addEventListener('focus', handleWindowFocus);

    return () => {
      window.clearInterval(intervalId);
      window.removeEventListener(
        'focus',
        handleWindowFocus,
      );
    };
  }, [user, loadActivityNotifications]);

  /**
   * Mở hoặc đóng bảng thông báo.
   * Khi mở sẽ đánh dấu các log hiện tại là đã xem.
   */
  const handleToggleNotifications = () => {
    if (showNotifications) {
      setShowNotifications(false);
      return;
    }

    setShowNotifications(true);
    setUnreadCount(0);

    void loadActivityNotifications(true);
  };

  if (!user || user.role !== Role.ADMIN) {
    return <Navigate to="/" replace />;
  }

  const initials = user.fullName
    .split(' ')
    .map((part) => part[0])
    .slice(-2)
    .join('')
    .toUpperCase();

  return (
    <div className="min-h-screen bg-[#f4f7fa] text-slate-900">
      <aside className="fixed left-0 top-0 z-20 flex h-screen w-[244px] flex-col border-r border-slate-200 bg-slate-50 text-slate-700">
        <div className="px-4 pb-5 pt-5">
          <Link
            to="/admin"
            className="flex items-center gap-2 text-lg font-extrabold tracking-tight text-[#1e1b4b]"
          >
            <span className="text-sm text-[#4338ca]" />
            <span className="text-xl font-bold text-indigo-800" >Academic Clarity</span>
          </Link>

          <p className="ml-5 mt-1 text-[11px] text-slate-500">
            System Administration
          </p>
        </div>

        <nav className="flex flex-1 flex-col gap-5 overflow-y-auto px-3 pb-4">
          {adminSections.map((section) => (
            <div key={section.title}>
              <p className="mb-2 px-3 text-[10px] font-extrabold uppercase tracking-[0.18em] text-slate-400">
                {section.title}
              </p>

              <div className="space-y-1">
                {section.items.map((item) => (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    end={item.end}
                    className={({ isActive }) =>
                      [
                        'flex items-center gap-3 rounded-xl px-3 py-3 text-sm font-bold transition',
                        isActive
                          ? 'bg-[#4338ca] text-white shadow-md shadow-indigo-200'
                          : 'text-slate-600 hover:bg-violet-50 hover:text-[#4338ca]',
                      ].join(' ')
                    }
                  >
                    <span className="flex h-7 w-7 items-center justify-center text-base">
                      {item.icon}
                    </span>

                    <span>{item.label}</span>
                  </NavLink>
                ))}
              </div>
            </div>
          ))}
        </nav>
      </aside>

      <div className="ml-[244px] min-h-screen">
        <header className="sticky top-0 z-10 flex h-16 items-center justify-between border-b border-slate-200 bg-white px-8 shadow-sm">
          <div />

          <div className="flex items-center gap-4 text-slate-600">
            <Link
              to="/"
              className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs font-bold text-slate-700 hover:bg-slate-50"
            >
              Back to app
            </Link>

            {/* Notification Bell */}
            <div className="relative">
              <button
                type="button"
                onClick={handleToggleNotifications}
                aria-label="Activity notifications"
                aria-expanded={showNotifications}
                className="relative flex h-9 w-9 items-center justify-center rounded-full text-slate-500 transition hover:bg-violet-50 hover:text-[#4338ca]"
              >
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  fill="none"
                  viewBox="0 0 24 24"
                  strokeWidth={1.8}
                  stroke="currentColor"
                  className="h-5 w-5"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M14.857 17.082a23.848 23.848 0 0 0 5.454-1.31A8.967 8.967 0 0 1 18 9.75V9A6 6 0 0 0 6 9v.75a8.967 8.967 0 0 1-2.312 6.022c1.733.64 3.56 1.085 5.455 1.31m5.714 0a24.255 24.255 0 0 1-5.714 0m5.714 0a3 3 0 1 1-5.714 0"
                  />
                </svg>

                {unreadCount > 0 && (
                  <span className="absolute -right-1 -top-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-red-600 px-1 text-[10px] font-bold leading-none text-white ring-2 ring-white">
                    {unreadCount > 99
                      ? '99+'
                      : unreadCount}
                  </span>
                )}
              </button>

              {/* Notification Popup */}
              {showNotifications && (
                <div className="absolute right-0 top-11 z-30 w-80 overflow-hidden rounded-xl border border-slate-200 bg-white shadow-xl">
                  <div className="border-b border-slate-100 px-4 py-3">
                    <div className="flex items-center justify-between gap-3">
                      <div>
                        <p className="text-sm font-extrabold text-slate-900">
                          Recent activity
                        </p>

                        <p className="mt-0.5 text-xs text-slate-500">
                          Latest admin and system events
                        </p>
                      </div>

                      <button
                        type="button"
                        onClick={() =>
                          void loadActivityNotifications(
                            true,
                          )
                        }
                        disabled={notificationsLoading}
                        className="shrink-0 text-xs font-bold text-[#4338ca] hover:underline disabled:cursor-not-allowed disabled:opacity-50"
                      >
                        {notificationsLoading
                          ? 'Loading...'
                          : 'Refresh'}
                      </button>
                    </div>
                  </div>

                  <div className="max-h-80 overflow-y-auto p-2">
                    {notificationsLoading &&
                      recentLogs.length === 0 ? (
                      <div className="px-3 py-8 text-center">
                        <p className="text-sm font-semibold text-slate-500">
                          Loading activity...
                        </p>
                      </div>
                    ) : notificationsError ? (
                      <div className="px-3 py-8 text-center">
                        <p className="text-sm font-semibold text-red-600">
                          Could not load activity logs.
                        </p>

                        <button
                          type="button"
                          onClick={() =>
                            void loadActivityNotifications(
                              true,
                            )
                          }
                          className="mt-2 text-xs font-bold text-[#4338ca] hover:underline"
                        >
                          Try again
                        </button>
                      </div>
                    ) : recentLogs.length === 0 ? (
                      <div className="px-3 py-8 text-center">
                        <p className="text-sm font-semibold text-slate-500">
                          No activity recorded
                        </p>
                      </div>
                    ) : (
                      recentLogs.map((log, index) => (
                        <div
                          key={`${getActivityFingerprint(
                            log,
                          )}-${index}`}
                          className="flex gap-3 rounded-lg px-3 py-3 hover:bg-slate-50"
                        >
                          <span
                            className={[
                              'mt-1.5 h-2.5 w-2.5 shrink-0 rounded-full',
                              activityDotClass[log.type],
                            ].join(' ')}
                          />

                          <div className="min-w-0 flex-1">
                            <div className="flex items-start justify-between gap-2">
                              <p className="text-sm font-bold text-slate-800">
                                {log.title}
                              </p>

                              <span className="shrink-0 rounded bg-slate-100 px-1.5 py-0.5 text-[9px] font-extrabold text-slate-500">
                                {
                                  activityTypeLabel[
                                  log.type
                                  ]
                                }
                              </span>
                            </div>

                            <p className="mt-1 truncate text-[11px] text-slate-500">
                              {log.ref}
                            </p>

                            <p className="mt-1 text-[10px] text-slate-400">
                              {log.time}
                            </p>
                          </div>
                        </div>
                      ))
                    )}
                  </div>

                  <Link
                    to="/admin/logs"
                    onClick={() =>
                      setShowNotifications(false)
                    }
                    className="block border-t border-slate-100 px-4 py-3 text-center text-xs font-bold text-[#4338ca] hover:bg-violet-50"
                  >
                    View all activity logs
                  </Link>
                </div>
              )}
            </div>

            <div className="flex items-center gap-3 border-l border-slate-200 pl-4">
              <div className="text-right">
                <p className="text-xs font-bold text-slate-900">
                  {user.fullName}
                </p>

                <p className="text-[10px] text-slate-500">
                  Administrator
                </p>
              </div>

              <Link
                to="/profile"
                className="flex h-8 w-8 items-center justify-center rounded-full bg-indigo-700 text-xs font-bold text-white"
              >
                {initials}
              </Link>
            </div>
          </div>
        </header>

        <main className="px-6 py-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
};

export default AdminLayout;
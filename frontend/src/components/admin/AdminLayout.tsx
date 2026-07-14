import { Link, NavLink, Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { Role } from '../../types/auth';
import { useState } from 'react';

const adminSections = [
  {
    title: 'OVERVIEW',
    items: [{ to: '/admin', label: 'Dashboard', icon: '▦', end: true }],
  },
  {
    title: 'CONTENT',
    items: [{ to: '/admin/repository', label: 'Article Repository', icon: '▤', end: false }],
  },
  {
    title: 'CUSTOMERS',
    items: [
      { to: '/admin/users', label: 'User Governance', icon: '◉', end: false },
      { to: '/admin/revenue', label: 'Revenue & Subscription', icon: '₫', end: false },
    ],
  },
  {
    title: 'SYSTEM',
    items: [
      { to: '/admin/pipelines', label: 'Data Pipeline', icon: '◈', end: false },
      { to: '/admin/logs', label: 'Activity Logs', icon: '▣', end: false },
    ],
  },
];

const AdminLayout = () => {
  const { user } = useAuth();
  const [showNotifications, setShowNotifications] = useState(false);

  if (!user || user.role !== Role.ADMIN) {
    return <Navigate to="/" replace />;
  }

  const initials = user.fullName
    .split(' ')
    .map((part) => part[0])
    .slice(-2)
    .join('')
    .toUpperCase();

  const notifications = [
    { title: 'OpenAlex sync completed', time: '10 mins ago', type: 'success' },
    { title: 'New user registered', time: '18 mins ago', type: 'info' },
    { title: 'Payment callback pending', time: '25 mins ago', type: 'warning' },
  ];
  return (
    <div className="min-h-screen bg-[#f4f7fa] text-slate-900">
      <aside className="fixed left-0 top-0 z-20 flex h-screen w-[244px] flex-col border-r border-slate-200 bg-slate-50 text-slate-700">
        <div className="px-4 pb-5 pt-5">
          <Link
            to="/admin"
            className="flex items-center gap-2 text-lg font-extrabold tracking-tight text-[#1e1b4b]"
          >
            <span className="text-sm text-[#4338ca]">✦</span>
            <span>AIS Admin</span>
          </Link>

          <p className="ml-5 mt-1 text-[11px] text-slate-500">Data Governance</p>
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
                    {({ isActive }) => (
                      <>
                        <span
                          className={[
                            'flex h-7 w-7 items-center justify-center rounded-lg text-sm',
                            isActive
                              ? 'bg-white text-[#4338ca]'
                              : 'bg-white text-[#4338ca] shadow-sm',
                          ].join(' ')}
                        >
                          {item.icon}
                        </span>

                        <span>{item.label}</span>
                      </>
                    )}
                  </NavLink>
                ))}
              </div>
            </div>
          ))}
        </nav>

        <div className="border-t border-slate-200 px-3 py-4">
          <NavLink
            to="/admin/settings"
            className={({ isActive }) =>
              [
                'flex items-center gap-3 rounded-xl px-3 py-3 text-sm font-bold transition',
                isActive
                  ? 'bg-[#4338ca] text-white shadow-md shadow-indigo-200'
                  : 'text-slate-600 hover:bg-violet-50 hover:text-[#4338ca]',
              ].join(' ')
            }
          >
            {({ isActive }) => (
              <>
                <span
                  className={[
                    'flex h-7 w-7 items-center justify-center rounded-lg text-sm',
                    isActive ? 'bg-white text-[#4338ca]' : 'bg-white text-[#4338ca] shadow-sm',
                  ].join(' ')}
                >
                  ⚙
                </span>
                <span>Settings</span>
              </>
            )}
          </NavLink>
        </div>
      </aside>

      <div className="ml-[244px] min-h-screen">
        <header className="sticky top-0 z-10 flex h-16 items-center justify-between border-b border-slate-200 bg-white px-8 shadow-sm">
          {/* Thanh search admin đã gỡ theo yêu cầu. */}
          <div />

          <div className="flex items-center gap-4 text-slate-600">
            <Link
              to="/"
              className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs font-bold text-slate-700 hover:bg-slate-50"
            >
              ← Back to app
            </Link>

            <div className="relative">
              <button
                onClick={() => setShowNotifications((current) => !current)}
                aria-label="Notifications"
                className="text-lg hover:text-[#4338ca]"
              >
                🔔
              </button>

              {showNotifications && (
                <div className="absolute right-0 top-10 z-30 w-80 rounded-xl border border-slate-200 bg-white shadow-lg">
                  <div className="border-b border-slate-100 px-4 py-3">
                    <p className="text-sm font-extrabold text-slate-900">Notifications</p>
                    <p className="text-xs text-slate-500">Latest admin and system alerts</p>
                  </div>

                  <div className="max-h-80 overflow-y-auto p-2">
                    {notifications.map((item) => (
                      <div key={item.title} className="rounded-lg px-3 py-3 hover:bg-slate-50">
                        <p className="text-sm font-bold text-slate-800">{item.title}</p>
                        <p className="mt-1 text-xs text-slate-500">{item.time}</p>
                      </div>
                    ))}
                  </div>

                  <Link
                    to="/admin/logs"
                    onClick={() => setShowNotifications(false)}
                    className="block border-t border-slate-100 px-4 py-3 text-center text-xs font-bold text-[#4338ca] hover:bg-violet-50"
                  >
                    View all activity logs
                  </Link>
                </div>
              )}
            </div>

            <div className="flex items-center gap-3 border-l border-slate-200 pl-4">
              <div className="text-right">
                <p className="text-xs font-bold text-slate-900">{user.fullName}</p>
                <p className="text-[10px] text-slate-500">System Overseer</p>
              </div>

              <Link
                to="/profile"
                className="flex h-8 w-8 items-center justify-center rounded-full bg-[#062b4f] text-xs font-bold text-white"
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
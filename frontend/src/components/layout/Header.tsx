import { useEffect, useState } from 'react';
import { Link, NavLink } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { AccessTier, Role } from '../../types/auth';
import { getUnreadCount } from '../../lib/api/notification';

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  isActive
    ? 'whitespace-nowrap border-b-2 border-indigo-700 pb-1 text-sm font-semibold text-indigo-700'
    : 'whitespace-nowrap pb-1 text-sm text-gray-700 hover:text-indigo-700';

const Header = () => {
  const { user } = useAuth();
  const [unread, setUnread] = useState(0);

  // Poll số thông báo chưa đọc cho badge chuông (mỗi 30s). Nhẹ, đủ "gần real-time".
  useEffect(() => {
    if (!user) return;
    let alive = true;
    const tick = () => getUnreadCount().then((r) => { if (alive) setUnread(r.count); }).catch(() => {});
    tick();
    const id = window.setInterval(tick, 30000);
    const onFocus = () => tick();
    window.addEventListener('focus', onFocus);
    return () => { alive = false; window.clearInterval(id); window.removeEventListener('focus', onFocus); };
  }, [user]);

  if (!user) return null;

  const initials = user.fullName
    .split(' ')
    .map((p) => p[0])
    .slice(-2)
    .join('');

  return (
    <header className="border-b border-gray-200 bg-white">
      <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
        <Link to="/" className="text-xl font-bold text-indigo-800">
          Academic Clarity
        </Link>

        <nav className="flex items-center gap-5">
          <NavLink to="/search" className={navLinkClass}>
            Search
          </NavLink>
          <NavLink to="/landscape" className={navLinkClass}>
            Research Landscape
          </NavLink>
          <NavLink to="/dashboard" className={navLinkClass}>
            Trend Dashboard
          </NavLink>
          <NavLink to="/overlap" className={navLinkClass}>
            Overlap Checker
          </NavLink>
          <NavLink to="/library" className={navLinkClass}>
            Saved Library
          </NavLink>
          {/* Admin Console đã chuyển vào trang Profile (chỉ admin) → bỏ khỏi header user. */}
        </nav>

        <div className="flex items-center gap-4">
          {/* Hiện CTA upgrade cho user BASIC (trừ Admin, vì Admin luôn full quyền - BR-26) */}
          {user.accessTier === AccessTier.BASIC && user.role !== Role.ADMIN && (
            <Link to="/pricing" className="rounded-full bg-indigo-50 px-3 py-1 text-xs font-medium text-indigo-700">
              Upgrade
            </Link>
          )}

          <Link to="/notifications" className="relative text-gray-500 hover:text-indigo-700" aria-label="Notifications">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" strokeWidth={1.8} stroke="currentColor" className="h-5 w-5">
              <path strokeLinecap="round" strokeLinejoin="round" d="M14.857 17.082a23.848 23.848 0 0 0 5.454-1.31A8.967 8.967 0 0 1 18 9.75V9A6 6 0 0 0 6 9v.75a8.967 8.967 0 0 1-2.312 6.022c1.733.64 3.56 1.085 5.455 1.31m5.714 0a24.255 24.255 0 0 1-5.714 0m5.714 0a3 3 0 1 1-5.714 0" />
            </svg>
            {unread > 0 && (
              <span className="absolute -right-1.5 -top-1.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-red-600 px-1 text-[10px] font-bold text-white">
                {unread > 99 ? '99+' : unread}
              </span>
            )}
          </Link>

          <Link
            to="/profile"
            className="flex h-8 w-8 items-center justify-center rounded-full bg-indigo-700 text-xs font-semibold text-white"
          >
            {initials}
          </Link>
        </div>
      </div>
    </header>
  );
};

export default Header;

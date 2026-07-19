import { useEffect, useState } from 'react';
import { Link, NavLink } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { AccessTier, Role } from '../../types/auth';
import { getUnreadCount } from '../../lib/api/notification';

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  isActive
    ? 'whitespace-nowrap border-b-2 border-indigo-700 pb-1 text-sm font-semibold text-indigo-700'
    : 'whitespace-nowrap pb-1 text-sm text-gray-700 hover:text-indigo-700';

// Menu mobile: link dạng block, active nền indigo thay vì gạch chân
const mobileNavLinkClass = ({ isActive }: { isActive: boolean }) =>
  isActive
    ? 'block rounded-md bg-indigo-50 px-3 py-2 text-sm font-semibold text-indigo-700'
    : 'block rounded-md px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 hover:text-indigo-700';

// Admin Console đã chuyển vào trang Profile (chỉ admin) → bỏ khỏi header user.
const NAV_ITEMS = [
  { to: '/search', label: 'Search' },
  { to: '/landscape', label: 'Research Landscape' },
  { to: '/dashboard', label: 'Journal & Keywords' },
  { to: '/overlap', label: 'Overlap Checker' },
  { to: '/latex', label: 'LaTeX Writer' },
  { to: '/library', label: 'Saved Library' },
];

const Header = () => {
  const { user } = useAuth();
  const [unread, setUnread] = useState(0);
  const [menuOpen, setMenuOpen] = useState(false);

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

  const showUpgrade = user.accessTier === AccessTier.BASIC && user.role !== Role.ADMIN;

  return (
    <header className="border-b border-gray-200 bg-white">
      <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3 sm:px-6 lg:px-8">
        <Link to="/" className="text-xl font-bold text-indigo-800">
          Academic Clarity
        </Link>

        {/* 6 link nav khá rộng nên chỉ hiện từ lg trở lên; dưới đó dùng menu hamburger */}
        <nav className="hidden items-center gap-5 lg:flex">
          {NAV_ITEMS.map((item) => (
            <NavLink key={item.to} to={item.to} className={navLinkClass}>
              {item.label}
            </NavLink>
          ))}
        </nav>

        <div className="flex items-center gap-3 lg:gap-4">
          {/* Hiện CTA upgrade cho user BASIC (trừ Admin, vì Admin luôn full quyền - BR-26) */}
          {showUpgrade && (
            <Link
              to="/pricing"
              className="hidden rounded-full bg-indigo-50 px-3 py-1 text-xs font-medium text-indigo-700 sm:inline-block"
            >
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

          <button
            type="button"
            onClick={() => setMenuOpen((v) => !v)}
            aria-label={menuOpen ? 'Đóng menu' : 'Mở menu'}
            aria-expanded={menuOpen}
            className="flex h-8 w-8 items-center justify-center rounded-md text-gray-600 hover:bg-gray-50 hover:text-indigo-700 lg:hidden"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} className="h-5 w-5">
              {menuOpen ? (
                <path strokeLinecap="round" strokeLinejoin="round" d="M6 18 18 6M6 6l12 12" />
              ) : (
                <path strokeLinecap="round" strokeLinejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25h16.5" />
              )}
            </svg>
          </button>
        </div>
      </div>

      {menuOpen && (
        <nav className="space-y-1 border-t border-gray-200 px-4 py-3 lg:hidden">
          {NAV_ITEMS.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={mobileNavLinkClass}
              onClick={() => setMenuOpen(false)}
            >
              {item.label}
            </NavLink>
          ))}

          {showUpgrade && (
            <Link
              to="/pricing"
              onClick={() => setMenuOpen(false)}
              className="block rounded-md px-3 py-2 text-sm font-medium text-indigo-700 hover:bg-indigo-50 sm:hidden"
            >
              Upgrade to Premium
            </Link>
          )}
        </nav>
      )}
    </header>
  );
};

export default Header;

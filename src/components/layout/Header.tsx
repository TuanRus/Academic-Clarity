import { Link, NavLink } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { AccessTier, Role } from '../../types/auth';
import { MOCK_USERS } from '../../mock/users';

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  isActive
    ? 'border-b-2 border-indigo-700 pb-1 text-sm font-semibold text-indigo-700'
    : 'pb-1 text-sm text-gray-700 hover:text-indigo-700';

const Header = () => {
  const { user, loginAsMock } = useAuth();
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

        <nav className="flex items-center gap-6">
          <NavLink to="/landscape" className={navLinkClass}>
            Research Landscape
          </NavLink>
          <NavLink to="/dashboard" className={navLinkClass}>
            Journal & Keywords
          </NavLink>
          <NavLink to="/library" className={navLinkClass}>
            Saved Library
          </NavLink>
          {/* FR-27/28: chỉ hiện cho role = ADMIN */}
          {user.role === Role.ADMIN && (
            <NavLink to="/admin" className={navLinkClass}>
              Admin
            </NavLink>
          )}
        </nav>

        <div className="flex items-center gap-4">
          {/* Hiện CTA upgrade cho user BASIC (trừ Admin, vì Admin luôn full quyền - BR-26) */}
          {user.accessTier === AccessTier.BASIC && user.role !== Role.ADMIN && (
            <Link to="/pricing" className="rounded-full bg-indigo-50 px-3 py-1 text-xs font-medium text-indigo-700">
              Upgrade
            </Link>
          )}

          <Link to="/notifications" className="text-gray-500 hover:text-indigo-700" aria-label="Notifications">
            🔔
          </Link>

          {/* Dev-only: chuyển user để test nhanh các tổ hợp role/tier - xoá khi tích hợp backend thật */}
          <select
            aria-label="Dev user switcher"
            className="rounded-md border border-gray-200 text-xs text-gray-700"
            value={user.id}
            onChange={(e) => loginAsMock(e.target.value)}
          >
            {MOCK_USERS.map((u) => (
              <option key={u.id} value={u.id}>
                {u.fullName} · {u.role} · {u.accessTier}
              </option>
            ))}
          </select>

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

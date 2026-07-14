import { useMemo, useState } from 'react';
import { AccessTier, Role, type User } from '../../types/auth';
import { MOCK_USERS } from '../../mock/users';

const roleBadgeClass: Record<Role, string> = {
  [Role.ADMIN]: 'bg-purple-100 text-purple-700',
  [Role.RESEARCHER]: 'bg-blue-100 text-blue-700',
  [Role.EDU]: 'bg-amber-100 text-amber-700',
};

const tierBadgeClass: Record<AccessTier, string> = {
  [AccessTier.PREMIUM]: 'bg-indigo-100 text-indigo-700',
  [AccessTier.BASIC]: 'bg-gray-100 text-gray-600',
};

// FR-27/28: khu vực quản trị - chỉ role ADMIN truy cập được (xem RequireAdmin).
// Quản lý danh sách user và xem nhanh số liệu tổng quan hệ thống.
// Dữ liệu vẫn lấy từ MOCK_USERS (chưa có backend thật) - các action set role/tier chỉ là demo cục bộ.
const AdminDashboardPage = () => {
  const [users, setUsers] = useState<User[]>(MOCK_USERS);

  const stats = useMemo(
    () => ({
      total: users.length,
      premium: users.filter((u) => u.accessTier === AccessTier.PREMIUM).length,
      basic: users.filter((u) => u.accessTier === AccessTier.BASIC).length,
      admins: users.filter((u) => u.role === Role.ADMIN).length,
    }),
    [users]
  );

  const updateUserTier = (userId: string, accessTier: AccessTier) => {
    setUsers((prev) =>
      prev.map((u) =>
        u.id === userId
          ? {
              ...u,
              accessTier,
              subscriptionValidUntil: accessTier === AccessTier.BASIC ? undefined : u.subscriptionValidUntil,
            }
          : u
      )
    );
  };

  const updateUserRole = (userId: string, role: Role) => {
    setUsers((prev) => prev.map((u) => (u.id === userId ? { ...u, role } : u)));
  };

  return (
    <div className="space-y-6">
      <div>
        <p className="text-xs font-medium uppercase tracking-wide text-gray-400">FR-27/28 · Khu vực quản trị</p>
        <h1 className="text-2xl font-bold text-gray-900">Admin Dashboard</h1>
        <p className="mt-1 text-sm text-gray-500">Quản lý người dùng và xem nhanh số liệu tổng quan hệ thống.</p>
      </div>

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
          <p className="text-xs font-medium text-gray-400">Total Users</p>
          <p className="mt-1 text-2xl font-bold text-gray-900">{stats.total}</p>
        </div>
        <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
          <p className="text-xs font-medium text-gray-400">Premium</p>
          <p className="mt-1 text-2xl font-bold text-indigo-700">{stats.premium}</p>
        </div>
        <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
          <p className="text-xs font-medium text-gray-400">Basic</p>
          <p className="mt-1 text-2xl font-bold text-gray-700">{stats.basic}</p>
        </div>
        <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
          <p className="text-xs font-medium text-gray-400">Admins</p>
          <p className="mt-1 text-2xl font-bold text-purple-700">{stats.admins}</p>
        </div>
      </div>

      <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
        <div className="border-b border-gray-200 px-6 py-4">
          <h2 className="text-lg font-semibold text-gray-800">User Management</h2>
          <p className="text-sm text-gray-500">Đổi role / access tier - chỉ ảnh hưởng dữ liệu demo trên FE.</p>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-gray-100 text-xs uppercase tracking-wide text-gray-400">
                <th className="px-6 py-3">User</th>
                <th className="px-6 py-3">Email</th>
                <th className="px-6 py-3">Role</th>
                <th className="px-6 py-3">Access Tier</th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => (
                <tr key={u.id} className="border-b border-gray-50 last:border-0">
                  <td className="px-6 py-3 font-medium text-gray-900">{u.fullName}</td>
                  <td className="px-6 py-3 text-gray-500">{u.email}</td>
                  <td className="px-6 py-3">
                    <select
                      aria-label={`Role của ${u.fullName}`}
                      value={u.role}
                      onChange={(e) => updateUserRole(u.id, e.target.value as Role)}
                      className={`rounded-full border-0 px-3 py-1 text-xs font-medium ${roleBadgeClass[u.role]}`}
                    >
                      {Object.values(Role).map((r) => (
                        <option key={r} value={r}>
                          {r}
                        </option>
                      ))}
                    </select>
                  </td>
                  <td className="px-6 py-3">
                    <select
                      aria-label={`Access tier của ${u.fullName}`}
                      value={u.accessTier}
                      onChange={(e) => updateUserTier(u.id, e.target.value as AccessTier)}
                      className={`rounded-full border-0 px-3 py-1 text-xs font-medium ${tierBadgeClass[u.accessTier]}`}
                    >
                      {Object.values(AccessTier).map((t) => (
                        <option key={t} value={t}>
                          {t}
                        </option>
                      ))}
                    </select>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};

export default AdminDashboardPage;

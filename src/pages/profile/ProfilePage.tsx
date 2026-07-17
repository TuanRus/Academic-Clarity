import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { AccessTier } from '../../types/auth';
import { getRoleLabel } from '../../lib/role';

// S-04 · Profile & Settings (FR-05) + Subscription section (BR-26..29, mới)
const ProfilePage = () => {
  const { user, logout, downgradeToBasic } = useAuth();

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [passwordError, setPasswordError] = useState('');
  const [passwordSuccess, setPasswordSuccess] = useState(false);

  if (!user) return null;

  // Demo only: chưa có backend lưu mật khẩu thật, chỉ validate phía FE rồi báo thành công.
  const handleChangePassword = (e: { preventDefault(): void }) => {
    e.preventDefault();
    setPasswordSuccess(false);

    if (!currentPassword) {
      setPasswordError('Vui lòng nhập mật khẩu hiện tại.');
      return;
    }
    if (newPassword.length < 8) {
      setPasswordError('Mật khẩu mới phải có ít nhất 8 ký tự.');
      return;
    }
    if (newPassword !== confirmPassword) {
      setPasswordError('Mật khẩu xác nhận không khớp.');
      return;
    }

    setPasswordError('');
    setPasswordSuccess(true);
    setCurrentPassword('');
    setNewPassword('');
    setConfirmPassword('');
  };

  const initials = user.fullName
    .split(' ')
    .map((p) => p[0])
    .slice(-2)
    .join('');

  return (
    <div className="grid gap-6 md:grid-cols-3">
      <div className="rounded-xl border border-gray-200 bg-white p-6 text-center shadow-sm">
        <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-indigo-700 text-lg font-semibold text-white">
          {initials}
        </div>
        <h2 className="mt-3 text-lg font-semibold text-gray-900">{user.fullName}</h2>
        <span className="mt-1 inline-block rounded-full bg-indigo-50 px-3 py-1 text-xs text-indigo-700">
          {getRoleLabel(user.role)}
        </span>

        <button
          onClick={logout}
          className="mt-6 w-full rounded-md border border-red-600 px-3 py-2 text-sm text-red-600 hover:bg-red-50"
        >
          Log Out
        </button>
      </div>

      <div className="space-y-6 md:col-span-2">
        <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <h3 className="text-lg font-semibold text-gray-800">Personal Details</h3>
          <p className="mt-1 text-sm text-gray-500">
            Update your personal information and institutional credentials below.
          </p>

          <div className="mt-4 space-y-3">
            <div>
              <label className="text-sm font-medium text-gray-700">Full Name</label>
              <input
                defaultValue={user.fullName}
                className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label className="text-sm font-medium text-gray-700">Institutional Email</label>
              <input
                disabled
                value={user.email}
                className="mt-1 w-full rounded-md border border-gray-200 bg-gray-100 px-3 py-2 text-sm text-gray-500"
              />
            </div>
          </div>

          <div className="mt-4 flex justify-end gap-2">
            <button className="rounded-md border border-gray-200 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50">
              Cancel
            </button>
            <button className="rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800">
              Save Changes
            </button>
          </div>
        </div>

        {/* Change Password (FR-05, mới) */}
        <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <h3 className="text-lg font-semibold text-gray-800">Change Password</h3>
          <p className="mt-1 text-sm text-gray-500">Cập nhật mật khẩu đăng nhập cho tài khoản của bạn.</p>

          {passwordError && (
            <div className="mt-3 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {passwordError}
            </div>
          )}
          {passwordSuccess && (
            <div className="mt-3 rounded-lg border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700">
              Đổi mật khẩu thành công.
            </div>
          )}

          <form className="mt-4 space-y-3" onSubmit={handleChangePassword}>
            <div>
              <label className="text-sm font-medium text-gray-700">Current Password</label>
              <input
                type="password"
                required
                value={currentPassword}
                onChange={(e) => setCurrentPassword(e.target.value)}
                autoComplete="current-password"
                className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label className="text-sm font-medium text-gray-700">New Password</label>
              <input
                type="password"
                required
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                autoComplete="new-password"
                className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label className="text-sm font-medium text-gray-700">Confirm New Password</label>
              <input
                type="password"
                required
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                autoComplete="new-password"
                className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
              />
            </div>

            <div className="flex justify-end">
              <button
                type="submit"
                className="rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800"
              >
                Update Password
              </button>
            </div>
          </form>
        </div>

        {/* Subscription - phản ánh access_tier, độc lập với role (mục 1, hệ quả #2) */}
        <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <h3 className="text-lg font-semibold text-gray-800">Subscription</h3>

          <div className="mt-3 flex items-center justify-between">
            <div>
              <p className="text-sm text-gray-700">
                Current plan: <span className="font-semibold text-gray-900">{user.accessTier}</span>
              </p>
              {user.accessTier === AccessTier.PREMIUM && user.subscriptionValidUntil && (
                <p className="text-xs text-gray-500">Valid until {user.subscriptionValidUntil}</p>
              )}
              {user.accessTier === AccessTier.BASIC && (
                <p className="text-xs text-gray-500">
                  Academic role ({getRoleLabel(user.role)}) không thay đổi khi nâng/hạ cấp gói.
                </p>
              )}
            </div>

            {user.accessTier === AccessTier.BASIC ? (
              <Link
                to="/pricing"
                className="rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800"
              >
                Upgrade to Premium
              </Link>
            ) : (
              <button
                onClick={downgradeToBasic}
                className="rounded-md border border-gray-200 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
              >
                Cancel Subscription
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default ProfilePage;

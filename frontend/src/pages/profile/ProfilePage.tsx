import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { AccessTier } from '../../types/auth';
import * as authApi from '../../lib/api/auth';

// S-04 · Profile & Settings (FR-05) + Subscription section (BR-26..29, mới)
const ProfilePage = () => {
  const { user, logout, refreshUser } = useAuth();

  const [fullName, setFullName] = useState('');
  const [institution, setInstitution] = useState('');
  const [saving, setSaving] = useState(false);
  const [savedMsg, setSavedMsg] = useState<string | null>(null);
  const [errMsg, setErrMsg] = useState<string | null>(null);

  // Đổi mật khẩu
  const [curPwd, setCurPwd] = useState('');
  const [newPwd, setNewPwd] = useState('');
  const [confirmPwd, setConfirmPwd] = useState('');
  const [pwdSaving, setPwdSaving] = useState(false);
  const [pwdMsg, setPwdMsg] = useState<string | null>(null);
  const [pwdErr, setPwdErr] = useState<string | null>(null);

  const handleChangePassword = async () => {
    setPwdMsg(null);
    setPwdErr(null);
    if (newPwd.length < 6) { setPwdErr('New password must be at least 6 characters.'); return; }
    if (newPwd !== confirmPwd) { setPwdErr('New password and confirmation do not match.'); return; }
    setPwdSaving(true);
    try {
      await authApi.changePassword(curPwd, newPwd);
      setPwdMsg('Password changed successfully.');
      setCurPwd(''); setNewPwd(''); setConfirmPwd('');
    } catch (e) {
      const msg = (e as { message?: string })?.message;
      setPwdErr(msg || 'Could not change password.');
    } finally {
      setPwdSaving(false);
    }
  };

  // Prefill từ DB (Full Name + Institution).
  useEffect(() => {
    authApi
      .getMe()
      .then((me) => {
        setFullName(me.fullname ?? '');
        setInstitution(me.institution ?? '');
      })
      .catch(() => {});
  }, []);

  if (!user) return null;

  const handleSave = async () => {
    setSaving(true);
    setSavedMsg(null);
    setErrMsg(null);
    try {
      await authApi.updateProfile({ fullname: fullName.trim(), institution: institution.trim() || null });
      await refreshUser(); // cập nhật tên trên Header từ DB
      setSavedMsg('Changes saved.');
    } catch {
      setErrMsg('Save failed. Please try again.');
    } finally {
      setSaving(false);
    }
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
          {user.role}
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
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label className="text-sm font-medium text-gray-700">Institution</label>
              <input
                value={institution}
                onChange={(e) => setInstitution(e.target.value)}
                placeholder="e.g., FPT University"
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

          {savedMsg && <p className="mt-3 text-sm text-green-600">{savedMsg}</p>}
          {errMsg && <p className="mt-3 text-sm text-red-600">{errMsg}</p>}

          <div className="mt-4 flex justify-end gap-2">
            <button
              type="button"
              disabled={saving}
              onClick={handleSave}
              className="rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800 disabled:opacity-60"
            >
              {saving ? 'Saving…' : 'Save Changes'}
            </button>
          </div>
        </div>

        {/* Change Password */}
        <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <h3 className="text-lg font-semibold text-gray-800">Change Password</h3>
          <p className="mt-1 text-sm text-gray-500">Enter your current password and a new one.</p>

          <div className="mt-4 space-y-3">
            <div>
              <label className="text-sm font-medium text-gray-700">Current Password</label>
              <input
                type="password"
                value={curPwd}
                onChange={(e) => setCurPwd(e.target.value)}
                className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label className="text-sm font-medium text-gray-700">New Password</label>
              <input
                type="password"
                value={newPwd}
                onChange={(e) => setNewPwd(e.target.value)}
                className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
              />
            </div>
            <div>
              <label className="text-sm font-medium text-gray-700">Confirm New Password</label>
              <input
                type="password"
                value={confirmPwd}
                onChange={(e) => setConfirmPwd(e.target.value)}
                className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
              />
            </div>
          </div>

          {pwdMsg && <p className="mt-3 text-sm text-green-600">{pwdMsg}</p>}
          {pwdErr && <p className="mt-3 text-sm text-red-600">{pwdErr}</p>}

          <div className="mt-4 flex justify-end">
            <button
              type="button"
              disabled={pwdSaving || !curPwd || !newPwd}
              onClick={handleChangePassword}
              className="rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800 disabled:opacity-60"
            >
              {pwdSaving ? 'Saving…' : 'Change Password'}
            </button>
          </div>
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
                  Your academic role ({user.role}) does not change when you upgrade or downgrade your plan.
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
              <span className="text-xs text-gray-500">
                Your Premium plan will end automatically when it expires.
              </span>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default ProfilePage;

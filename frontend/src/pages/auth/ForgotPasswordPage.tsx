import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import * as authApi from '../../lib/api/auth';
import { ApiError } from '../../lib/http';

// S-03 · Quên mật khẩu (FR-03): send OTP → verify OTP → reset password.
const ForgotPasswordPage = () => {
  const navigate = useNavigate();
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [email, setEmail] = useState('');
  const [otp, setOtp] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);

  const wrap = async (fn: () => Promise<void>) => {
    setLoading(true);
    setError(null);
    setInfo(null);
    try {
      await fn();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Không kết nối được máy chủ.');
    } finally {
      setLoading(false);
    }
  };

  const sendOtp = () =>
    wrap(async () => {
      await authApi.forgotPasswordSendOtp(email.trim());
      setInfo('An OTP has been sent to your email.');
      setStep(2);
    });

  const verifyOtp = () =>
    wrap(async () => {
      await authApi.forgotPasswordVerifyOtp(email.trim(), otp.trim());
      setStep(3);
    });

  const reset = () =>
    wrap(async () => {
      await authApi.forgotPasswordReset(email.trim(), newPassword);
      navigate('/login', { replace: true });
    });

  return (
    <div className="mx-auto mt-16 max-w-md rounded-xl border border-gray-200 bg-white p-8 shadow-sm">
      <h1 className="text-2xl font-bold text-gray-900">Forgot Password</h1>
      <p className="mt-1 text-sm text-gray-500">
        {step === 1 && 'Enter your account email to receive a recovery OTP.'}
        {step === 2 && 'Enter the 6-digit OTP sent to your email.'}
        {step === 3 && 'Set a new password for your account.'}
      </p>

      <div className="mt-5 space-y-3">
        <div>
          <label className="text-sm font-medium text-gray-700">Email</label>
          <input
            type="email"
            value={email}
            disabled={step !== 1}
            onChange={(e) => setEmail(e.target.value)}
            className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm disabled:bg-gray-100"
          />
        </div>

        {step === 2 && (
          <div>
            <label className="text-sm font-medium text-gray-700">OTP Code</label>
            <input
              value={otp}
              onChange={(e) => setOtp(e.target.value)}
              placeholder="6 digits"
              className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
            />
          </div>
        )}

        {step === 3 && (
          <div>
            <label className="text-sm font-medium text-gray-700">New Password</label>
            <input
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
            />
          </div>
        )}

        {info && <p className="text-sm text-green-600">{info}</p>}
        {error && <p className="text-sm text-red-600">{error}</p>}

        <button
          type="button"
          disabled={loading}
          onClick={step === 1 ? sendOtp : step === 2 ? verifyOtp : reset}
          className="w-full rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800 disabled:opacity-60"
        >
          {loading ? 'Processing…' : step === 1 ? 'Send OTP' : step === 2 ? 'Verify OTP' : 'Reset Password'}
        </button>
      </div>

      <p className="mt-4 text-sm text-gray-500">
        <Link to="/login" className="text-indigo-700 hover:underline">
          ← Back to login
        </Link>
      </p>
    </div>
  );
};

export default ForgotPasswordPage;

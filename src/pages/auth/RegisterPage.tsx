import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { isEduEmail, inferRoleFromEmail } from '../../lib/email';
import { PREMIUM_MONTHLY_PRICE, formatVnd } from '../../lib/pricing';
import { getRoleLabel } from '../../lib/role';

// S-01 · Register Screen (FR-01)
// Role KHÔNG còn cho chọn tay: tự suy ra từ domain email (mail .edu -> EDU, gộp chung
// Lecturer + Student; còn lại -> RESEARCHER). Role là CỐ ĐỊNH cho danh tính học thuật -
// KHÔNG đổi khi access_tier thay đổi sau này (xem BR-29).
const RegisterPage = () => {
  const navigate = useNavigate();
  const { register } = useAuth();

  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Xác thực OTP cho MỌI email khi đăng ký (demo, chưa có backend gửi mail thật).
  // Áp dụng cho tất cả role: vì RESEARCHER (email không .edu) vẫn có thể mua Premium,
  // không chỉ riêng email .edu mới cần xác minh.
  const [otp, setOtp] = useState('');
  const [sentOtp, setSentOtp] = useState<string | null>(null);
  const [otpVerified, setOtpVerified] = useState(false);
  const [otpError, setOtpError] = useState('');

  const isEdu = isEduEmail(email);
  const detectedRole = email ? inferRoleFromEmail(email) : null;
  const needsOtp = !!email && !otpVerified;

  const resetOtpState = () => {
    setSentOtp(null);
    setOtpVerified(false);
    setOtp('');
    setOtpError('');
  };

  const handleEmailChange = (value: string) => {
    setEmail(value);
    resetOtpState();
  };

  const handleSendOtp = () => {
    // Demo only: sinh mã 6 số tại FE thay cho việc gửi email thật.
    const code = Math.floor(100000 + Math.random() * 900000).toString();
    setSentOtp(code);
    setOtpVerified(false);
    setOtp('');
    setOtpError('');
  };

  const handleVerifyOtp = () => {
    if (otp === sentOtp) {
      setOtpVerified(true);
      setOtpError('');
    } else {
      setOtpError('Mã OTP không đúng. Vui lòng kiểm tra lại.');
    }
  };

  const handleSubmit = async (e: { preventDefault(): void }) => {
    e.preventDefault();
    setError('');

    if (password.length < 8) {
      setError('Mật khẩu phải có ít nhất 8 ký tự.');
      return;
    }
    if (password !== confirmPassword) {
      setError('Mật khẩu xác nhận không khớp.');
      return;
    }
    if (needsOtp) {
      setError('Vui lòng xác thực mã OTP gửi tới email của bạn trước khi đăng ký.');
      return;
    }

    setIsSubmitting(true);
    try {
      await register(fullName, email, password);
      navigate('/login');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-100 px-4">
      <div className="w-full max-w-md rounded-xl border border-gray-200 bg-white p-8 shadow-sm">
        <h1 className="text-2xl font-bold text-gray-900">Create Your Account</h1>
        <p className="mt-1 text-sm text-gray-500">
          Join the institutional network to access courses, research tracks, and administrative tools.
        </p>

        {error && (
          <div className="mt-4 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {error}
          </div>
        )}

        <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
          <div>
            <label className="text-sm font-medium text-gray-700">Full Name</label>
            <input
              required
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              placeholder="e.g., Hoàng Tiến Đạt"
              className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
            />
          </div>

          <div>
            <label className="text-sm font-medium text-gray-700">Institutional Email</label>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => handleEmailChange(e.target.value)}
              placeholder="username@university.edu.vn"
              className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
            />
            {detectedRole && (
              <p className="mt-1 text-xs text-gray-500">
                Academic role tự nhận diện:{' '}
                <span className="font-medium text-indigo-700">{getRoleLabel(detectedRole)}</span>
                {isEdu && (
                  <>
                    {' '}
                    · Email .edu được áp giá Premium ưu đãi{' '}
                    <span className="font-medium text-indigo-700">
                      {formatVnd(PREMIUM_MONTHLY_PRICE.edu)}/tháng
                    </span>{' '}
                    (thay vì {formatVnd(PREMIUM_MONTHLY_PRICE.standard)}).
                  </>
                )}
              </p>
            )}
          </div>

          {/* Xác thực OTP cho mọi email khi đăng ký (demo, chưa nối backend gửi mail thật) */}
          {!!email && (
            <div className="rounded-md border border-amber-200 bg-amber-50 p-3">
              {otpVerified ? (
                <p className="flex items-center gap-1 text-sm font-medium text-green-700">
                  ✓ Email đã được xác thực OTP.
                </p>
              ) : (
                <>
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-sm font-medium text-gray-700">Mã OTP xác thực email</span>
                    <button
                      type="button"
                      onClick={handleSendOtp}
                      className="rounded-md border border-indigo-700 px-3 py-1 text-xs font-medium text-indigo-700 hover:bg-indigo-50"
                    >
                      {sentOtp ? 'Gửi lại mã' : 'Gửi mã OTP'}
                    </button>
                  </div>

                  {sentOtp && (
                    <>
                      <p className="mt-2 text-xs text-gray-500">
                        Demo: mã OTP đã "gửi" tới email của bạn là{' '}
                        <span className="font-semibold text-gray-700">{sentOtp}</span> (chưa nối hệ thống gửi mail
                        thật).
                      </p>
                      <div className="mt-2 flex gap-2">
                        <input
                          inputMode="numeric"
                          maxLength={6}
                          value={otp}
                          onChange={(e) => setOtp(e.target.value.replace(/\D/g, ''))}
                          placeholder="Nhập mã 6 số"
                          className="w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
                        />
                        <button
                          type="button"
                          onClick={handleVerifyOtp}
                          className="shrink-0 rounded-md bg-indigo-700 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-800"
                        >
                          Xác thực
                        </button>
                      </div>
                      {otpError && <p className="mt-1 text-xs text-red-600">{otpError}</p>}
                    </>
                  )}
                </>
              )}
            </div>
          )}

          <div>
            <label className="text-sm font-medium text-gray-700">Password</label>
            <input
              type="password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="new-password"
              className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
            />
          </div>

          <div>
            <label className="text-sm font-medium text-gray-700">Confirm Password</label>
            <input
              type="password"
              required
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              autoComplete="new-password"
              className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
            />
          </div>

          <button
            type="submit"
            disabled={isSubmitting || needsOtp}
            title={needsOtp ? 'Vui lòng xác thực OTP email trước khi đăng ký' : undefined}
            className="w-full rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSubmitting ? 'Đang tạo tài khoản…' : 'Register Account'}
          </button>
        </form>

        <p className="mt-4 text-center text-sm text-gray-700">
          Already have an account?{' '}
          <Link to="/login" className="font-medium text-indigo-700">
            Sign In
          </Link>
        </p>
      </div>
    </div>
  );
};

export default RegisterPage;

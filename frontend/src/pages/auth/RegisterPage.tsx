import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Role } from '../../types/auth';
import { sendOtp, register } from '../../lib/api/auth';
import { ApiError } from '../../lib/http';

// Chỉ tài khoản email học thuật (.edu / .edu.vn) mới được chọn vai trò Student / Lecturer.
// Backend gán RoleId 3 (edu user) cho mọi email edu — Student/Lecturer ở đây chỉ là nhãn hiển thị.
const EDU_ROLE_OPTIONS: { value: Role; label: string }[] = [
  { value: Role.STUDENT, label: 'Student' },
  { value: Role.LECTURER, label: 'Lecturer' },
];

const isEduEmail = (email: string): boolean => {
  const e = email.trim().toLowerCase();
  return e.endsWith('.edu') || e.endsWith('.edu.vn');
};

// S-01 · Register Screen (FR-01)
// Luồng 2 bước theo backend: gửi OTP -> nhập OTP + tạo tài khoản (lưu DB).
const RegisterPage = () => {
  const navigate = useNavigate();

  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<Role>(Role.STUDENT);

  const [otpSent, setOtpSent] = useState(false);
  const [otpCode, setOtpCode] = useState('');

  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const edu = isEduEmail(email);

  // Bước 1: yêu cầu backend sinh & gửi mã OTP về email.
  const handleSendOtp = async (e: { preventDefault(): void }) => {
    e.preventDefault();
    setError(null);
    setInfo(null);
    setSubmitting(true);
    try {
      await sendOtp(email.trim());
      setOtpSent(true);
      setInfo('Mã OTP đã được gửi tới email của bạn. Vui lòng kiểm tra hộp thư.');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Không gửi được mã OTP.');
    } finally {
      setSubmitting(false);
    }
  };

  // Bước 2: đối chiếu OTP và tạo tài khoản (ghi vào DB).
  const handleRegister = async (e: { preventDefault(): void }) => {
    e.preventDefault();
    setError(null);
    setInfo(null);
    setSubmitting(true);
    try {
      await register({
        fullname: fullName.trim(),
        email: email.trim(),
        password,
        otpCode: otpCode.trim(),
      });
      navigate('/login');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Đăng ký tài khoản thất bại.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-100 px-4">
      <div className="w-full max-w-md rounded-xl border border-gray-200 bg-white p-8 shadow-sm">
        <h1 className="text-2xl font-bold text-gray-900">Create Your Account</h1>
        <p className="mt-1 text-sm text-gray-500">
          Join the institutional network to access courses, research tracks, and administrative tools.
        </p>

        <form className="mt-6 space-y-4" onSubmit={otpSent ? handleRegister : handleSendOtp}>
          <div>
            <label className="text-sm font-medium text-gray-700">Full Name</label>
            <input
              required
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              disabled={otpSent}
              placeholder="e.g., Hoàng Tiến Đạt"
              className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm disabled:bg-gray-50 disabled:text-gray-500"
            />
          </div>

          <div>
            <label className="text-sm font-medium text-gray-700">Institutional Email</label>
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={otpSent}
              placeholder="username@university.edu.vn"
              className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm disabled:bg-gray-50 disabled:text-gray-500"
            />
          </div>

          <div>
            <label className="text-sm font-medium text-gray-700">Password</label>
            <input
              type="password"
              required
              minLength={6}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              disabled={otpSent}
              className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm disabled:bg-gray-50 disabled:text-gray-500"
            />
          </div>

          {/* Vai trò chỉ hiện khi dùng email học thuật (.edu / .edu.vn) */}
          {edu && (
            <div>
              <span className="text-sm font-medium text-gray-700">Institutional Role</span>
              <div className="mt-1 grid grid-cols-2 gap-2">
                {EDU_ROLE_OPTIONS.map((opt) => (
                  <button
                    key={opt.value}
                    type="button"
                    onClick={() => setRole(opt.value)}
                    disabled={otpSent}
                    className={`rounded-md border px-3 py-2 text-sm disabled:opacity-60 ${
                      role === opt.value
                        ? 'border-indigo-700 bg-indigo-50 text-indigo-700'
                        : 'border-gray-200 text-gray-700'
                    }`}
                  >
                    {opt.label}
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* Bước 2: nhập mã OTP nhận qua email */}
          {otpSent && (
            <div>
              <label className="text-sm font-medium text-gray-700">OTP Code</label>
              <input
                required
                inputMode="numeric"
                maxLength={6}
                value={otpCode}
                onChange={(e) => setOtpCode(e.target.value)}
                placeholder="6 chữ số"
                className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm tracking-widest"
              />
            </div>
          )}

          {error && <p className="text-sm text-red-600">{error}</p>}
          {info && <p className="text-sm text-green-600">{info}</p>}

          <button
            type="submit"
            disabled={submitting}
            className="w-full rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800 disabled:opacity-50"
          >
            {submitting
              ? 'Đang xử lý…'
              : otpSent
                ? 'Register Account'
                : 'Send OTP'}
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

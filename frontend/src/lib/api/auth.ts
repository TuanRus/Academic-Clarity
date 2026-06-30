import { apiPost, apiGet, apiPut } from '../http';

// Tầng service cho controller api/Auth + api/subscriptions (nối auth thật vào BE).

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  email: string;
}

export interface SubscriptionStatus {
  isPremiumActive: boolean;
  planId: number | null;
  planName: string;
  status: string;
  startedAt: string | null;
  endsAt: string | null;
}

export interface RegisterPayload {
  fullname: string;
  email: string;
  password: string;
  otpCode: string;
  institution?: string;
}

/** POST /api/Auth/send-otp — sinh OTP đăng ký, gửi về email. Trả về email đã kích hoạt. */
export function sendOtp(email: string): Promise<string> {
  return apiPost<string>('/Auth/send-otp', { email });
}

/** POST /api/Auth/register — tạo tài khoản mới sau khi đối chiếu OTP. Trả về email đã đăng ký. */
export function register(payload: RegisterPayload): Promise<string> {
  return apiPost<string>('/Auth/register', payload);
}

// --- Quên mật khẩu (3 bước) ---
/** Bước 0: gửi OTP khôi phục về email. */
export function forgotPasswordSendOtp(email: string): Promise<string> {
  return apiPost<string>('/Auth/forgot-password/send-otp', { email });
}
/** Bước 1: xác thực OTP khôi phục. */
export function forgotPasswordVerifyOtp(email: string, otpCode: string): Promise<string> {
  return apiPost<string>('/Auth/forgot-password/verify-otp', { email, otpCode });
}
/** Bước 2: đặt lại mật khẩu mới. */
export function forgotPasswordReset(email: string, newPassword: string): Promise<string> {
  return apiPost<string>('/Auth/forgot-password/reset-password', { email, newPassword });
}

export interface UserProfile {
  userId: number;
  email: string;
  fullname: string;
  roleId: number;
  institution: string | null;
}

/** GET /api/Auth/me — hồ sơ user hiện tại đọc thẳng từ DB theo token. */
export function getMe(): Promise<UserProfile> {
  return apiGet<UserProfile>('/Auth/me');
}

/** PUT /api/Auth/me — cập nhật Full Name + Institution. */
export function updateProfile(input: { fullname: string; institution?: string | null }): Promise<UserProfile> {
  return apiPut<UserProfile>('/Auth/me', input);
}

/** PUT /api/Auth/change-password — đổi mật khẩu khi đã đăng nhập (cần mật khẩu cũ). */
export function changePassword(currentPassword: string, newPassword: string): Promise<string> {
  return apiPut<string>('/Auth/change-password', { currentPassword, newPassword });
}

/** POST /api/Auth/login — trả access/refresh token. */
export function login(email: string, password: string): Promise<LoginResponse> {
  return apiPost<LoginResponse>('/Auth/login', { email, password });
}

/** POST /api/Auth/logout — thu hồi refresh token (best-effort).
 *  BE cần cả accessToken (để lấy jwtId tìm refresh token) lẫn refreshToken. */
export function logout(accessToken: string, refreshToken: string): Promise<unknown> {
  return apiPost('/Auth/logout', { accessToken, refreshToken });
}

/** GET /api/subscriptions/status — tier hiện tại của user (PREMIUM/BASIC). */
export function getSubscriptionStatus(): Promise<SubscriptionStatus> {
  return apiGet<SubscriptionStatus>('/subscriptions/status');
}

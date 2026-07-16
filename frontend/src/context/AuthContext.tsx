import { createContext, useState, useEffect, type ReactNode } from 'react';
import { type User, Role, AccessTier } from '../types/auth';
import { setAuthToken, getAuthToken } from '../lib/http';
import * as authApi from '../lib/api/auth';

interface AuthContextValue {
  user: User | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  /** Cập nhật tier sau khi thanh toán thành công (BR-27a). */
  upgradeToPremium: (validUntil: string) => void;
  /** Hạ về BASIC khi hết hạn (BR-29) — role KHÔNG đổi. */
  downgradeToBasic: () => void;
  /** Đồng bộ lại user từ BG (gọi sau khi thanh toán PayOS để lấy tier/role mới từ DB). */
  refreshUser: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);

const REFRESH_KEY = 'stt_refresh_token';

// JWT của BE dùng RoleId (số). Map sang Role hiển thị của FE.
function mapRoleId(roleId: string | number | undefined): Role {
  switch (String(roleId)) {
    case '1': return Role.ADMIN;
    case '2': return Role.RESEARCHER; // nâng cấp qua thanh toán
    case '3': return Role.STUDENT;    // edu user (Student/Lecturer)
    default: return Role.USER;        // 4 = user thường
  }
}

interface JwtClaims {
  userId: string;
  email: string;
  roleId: string;
  fullName: string;
}

// Giải mã payload JWT (base64url) — không verify chữ ký (chỉ đọc claim phía client).
function decodeJwt(token: string): JwtClaims | null {
  try {
    const payload = token.split('.')[1];
    const json = JSON.parse(decodeURIComponent(escape(atob(payload.replace(/-/g, '+').replace(/_/g, '/')))));
    return {
      userId: json['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ?? json.nameid ?? '',
      email: json['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ?? json.email ?? '',
      roleId: json['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ?? json.role ?? '4',
      fullName: json.fullName ?? '',
    };
  } catch {
    return null;
  }
}

// Dựng User từ token + gọi subscription status để biết tier.
async function buildUser(token: string): Promise<User | null> {
  const claims = decodeJwt(token);
  if (!claims) return null;

  // GET /Auth/me là CHÂN LÝ xác thực: http.ts sẽ tự refresh nếu access token hết hạn
  // (dùng refresh token). Nếu vẫn thất bại (refresh cũng hết hạn / token không hợp lệ)
  // → phiên đã chết → trả null để buộc đăng nhập lại (không dựng phiên giả từ token cũ).
  let me: Awaited<ReturnType<typeof authApi.getMe>>;
  try {
    me = await authApi.getMe();
  } catch {
    return null;
  }

  let tier: AccessTier = AccessTier.BASIC;
  let validUntil: string | undefined;
  try {
    const sub = await authApi.getSubscriptionStatus();
    if (sub.isPremiumActive) {
      tier = AccessTier.PREMIUM;
      validUntil = sub.endsAt ?? undefined;
    }
  } catch {
    // lỗi lấy tier → coi như chưa premium (không ảnh hưởng trạng thái đăng nhập)
  }

  return {
    id: claims.userId,
    fullName: me.fullname || claims.fullName || claims.email.split('@')[0] || claims.email,
    email: me.email || claims.email,
    role: mapRoleId(String(me.roleId ?? claims.roleId)),
    accessTier: tier,
    subscriptionValidUntil: validUntil,
  };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  // Khôi phục phiên từ token đã lưu — CHỈ khi /Auth/me xác thực OK (kể cả sau auto-refresh).
  // Phiên đã chết (token hết hạn + refresh hết hạn) → xóa sạch token, không auto-login giả.
  useEffect(() => {
    const token = getAuthToken();
    if (!token) { setLoading(false); return; }
    buildUser(token)
      .then((u) => {
        if (u) {
          setUser(u);
        } else {
          setAuthToken(null);
          localStorage.removeItem(REFRESH_KEY);
        }
      })
      .finally(() => setLoading(false));
  }, []);

  const login = async (email: string, password: string) => {
    const res = await authApi.login(email, password);
    setAuthToken(res.accessToken);
    localStorage.setItem(REFRESH_KEY, res.refreshToken);
    const u = await buildUser(res.accessToken);
    setUser(u);
    window.dispatchEvent(new Event('stt-auth-changed'));
  };

  const refreshUser = async () => {
    const token = getAuthToken();
    if (!token) return;
    const u = await buildUser(token);
    if (u) setUser(u);
  };

  const logout = () => {
    const refresh = localStorage.getItem(REFRESH_KEY);
    const access = getAuthToken();
    if (refresh && access) authApi.logout(access, refresh).catch(() => {});
    setAuthToken(null);
    localStorage.removeItem(REFRESH_KEY);
    setUser(null);
    window.dispatchEvent(new Event('stt-auth-changed'));
  };

  const upgradeToPremium = (validUntil: string) => {
    setUser((prev) =>
      prev ? { ...prev, accessTier: AccessTier.PREMIUM, subscriptionValidUntil: validUntil } : prev
    );
  };

  const downgradeToBasic = () => {
    setUser((prev) =>
      prev ? { ...prev, accessTier: AccessTier.BASIC, subscriptionValidUntil: undefined } : prev
    );
  };

  return (
    <AuthContext.Provider value={{ user, loading, login, logout, upgradeToPremium, downgradeToBasic, refreshUser }}>
      {children}
    </AuthContext.Provider>
  );
}

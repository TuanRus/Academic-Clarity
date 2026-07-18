import { createContext, useState, useEffect, type ReactNode } from 'react';
import { type User, Role, AccessTier } from '../types/auth';
import { setAuthToken, getAuthToken } from '../lib/http';
import * as authApi from '../lib/api/auth';

interface AuthContextValue {
  user: User | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<User | null>;
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

  let tier: AccessTier = AccessTier.BASIC;
  let validUntil: string | undefined;
  try {
    const sub = await authApi.getSubscriptionStatus();
    if (sub.isPremiumActive) {
      tier = AccessTier.PREMIUM;
      validUntil = sub.endsAt ?? undefined;
    }
  } catch {
    // token hết hạn / lỗi → coi như chưa premium
  }

  // Nguồn chuẩn là DB (GET /Auth/me): đảm bảo Full Name & Role luôn khớp DB,
  // không phụ thuộc token cũ. Nếu lỗi thì fallback về claim trong token.
  let fullName = claims.fullName || claims.email.split('@')[0] || claims.email;
  let roleId = claims.roleId;
  let email = claims.email;
  try {
    const me = await authApi.getMe();
    fullName = me.fullname || fullName;
    roleId = String(me.roleId);
    email = me.email || email;
  } catch {
    // dùng dữ liệu từ token
  }

  return {
    id: claims.userId,
    fullName,
    email,
    role: mapRoleId(roleId),
    accessTier: tier,
    subscriptionValidUntil: validUntil,
  };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  // Khôi phục phiên từ token đã lưu (nếu còn hạn).
  useEffect(() => {
    const token = getAuthToken();
    if (!token) { setLoading(false); return; }
    buildUser(token)
      .then((u) => { if (u) setUser(u); else setAuthToken(null); })
      .finally(() => setLoading(false));
  }, []);

  const login = async (email: string, password: string) => {
    const res = await authApi.login(email, password);
    setAuthToken(res.accessToken);
    localStorage.setItem(REFRESH_KEY, res.refreshToken);
    const u = await buildUser(res.accessToken);
    setUser(u);
    window.dispatchEvent(new Event('stt-auth-changed'));
    return u;
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

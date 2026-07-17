import { createContext, useState, type ReactNode } from 'react';
import { type User, AccessTier } from '../types/auth';
import { MOCK_USERS } from '../mock/users';
import { inferRoleFromEmail } from '../lib/email';

interface AuthContextValue {
  user: User | null;
  login: (email: string, password: string) => Promise<void>;
  /** S-01: tạo account mới (demo, mock array) - role tự suy ra từ email (xem inferRoleFromEmail). */
  register: (fullName: string, email: string, password: string) => Promise<void>;
  logout: () => void;
  /** Demo only: đổi nhanh user để test các tổ hợp role/tier */
  loginAsMock: (userId: string) => void;
  /** Demo only: mô phỏng IPN SUCCESS -> set PREMIUM (BR-27a) */
  upgradeToPremium: (validUntil: string) => void;
  /** Demo only: mô phỏng BR-29 - hết hạn -> hạ về BASIC, role KHÔNG đổi */
  downgradeToBasic: () => void;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  // Mặc định login sẵn 1 user (Student/BASIC) để xem được toàn bộ luồng ngay.
  const [user, setUser] = useState<User | null>(MOCK_USERS[0]);

  const login = async (email: string, _password: string) => {
    const found = MOCK_USERS.find((u) => u.email === email) ?? MOCK_USERS[0];
    setUser(found);
  };

  const register = async (fullName: string, email: string, _password: string) => {
    const newUser: User = {
      id: `u${Date.now()}`,
      fullName,
      email,
      role: inferRoleFromEmail(email),
      accessTier: AccessTier.BASIC,
    };
    // Demo only: chưa có backend thật, push thẳng vào mock array để login/dev-switcher dùng được ngay.
    MOCK_USERS.push(newUser);
  };

  const logout = () => setUser(null);

  const loginAsMock = (userId: string) => {
    const found = MOCK_USERS.find((u) => u.id === userId);
    if (found) setUser(found);
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
    <AuthContext.Provider
      value={{ user, login, register, logout, loginAsMock, upgradeToPremium, downgradeToBasic }}
    >
      {children}
    </AuthContext.Provider>
  );
}

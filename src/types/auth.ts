// Trục 1: Role - chỉ dùng để hiển thị danh tính học thuật (S-03, S-04)
// và gate khu vực quản trị (FR-27/28). KHÔNG dùng để quyết định feature access.
// EDU = gộp chung LECTURER + STUDENT, tự suy ra từ domain email khi đăng ký (mail .edu) -
// xem inferRoleFromEmail() trong src/lib/email.ts. Không còn cho user chọn role ở Register.
export const Role = {
  RESEARCHER: 'RESEARCHER',
  EDU: 'EDU',
  ADMIN: 'ADMIN',
} as const;
export type Role = typeof Role[keyof typeof Role];

// Trục 2: AccessTier - quyết định toàn bộ feature access cho FR-11..22
// thông qua tier_permissions (xem src/lib/permissions.ts)
export const AccessTier = {
  BASIC: 'BASIC',
  PREMIUM: 'PREMIUM',
} as const;
export type AccessTier = typeof AccessTier[keyof typeof AccessTier];

export interface User {
  id: string;
  fullName: string;
  email: string;
  role: Role;
  accessTier: AccessTier;
  /** Chỉ có giá trị khi accessTier = PREMIUM (User Subscription ACTIVE) */
  subscriptionValidUntil?: string;
}

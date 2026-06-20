import { AccessTier, Role, type User } from '../types/auth';

/**
 * Danh sách user demo để test các tổ hợp role/tier khác nhau
 * (đổi qua dropdown "dev user switcher" trong Header).
 */
export const MOCK_USERS: User[] = [
  {
    id: 'u1',
    fullName: 'Hoàng Tiến Đạt',
    email: 'dat.ht@university.edu.vn',
    role: Role.EDU,
    accessTier: AccessTier.BASIC,
  },
  {
    id: 'u2',
    fullName: 'Dr. Sarah Jenkins',
    email: 'sarah.jenkins@university.edu.vn',
    role: Role.RESEARCHER,
    accessTier: AccessTier.PREMIUM,
    subscriptionValidUntil: '2026-12-31',
  },
  {
    id: 'u5',
    fullName: 'Jane Cooper',
    email: 'jane.cooper@gmail.com',
    // User cá nhân (email không .edu) chưa nâng cấp - giá Premium chuẩn 99.000đ
    // (khác u2 đã PREMIUM, dùng để test luồng nâng cấp với giá standard).
    role: Role.RESEARCHER,
    accessTier: AccessTier.BASIC,
  },
  {
    id: 'u3',
    fullName: 'Mark R. Thorne',
    email: 'mark.thorne@university.edu.vn',
    role: Role.EDU,
    accessTier: AccessTier.BASIC,
  },
  {
    id: 'u4',
    fullName: 'System Admin',
    email: 'admin@university.edu.vn',
    role: Role.ADMIN,
    // Minh hoạ BR-26: Admin tier = BASIC nhưng vẫn full quyền sản phẩm nhờ ngoại lệ.
    accessTier: AccessTier.BASIC,
  },
];

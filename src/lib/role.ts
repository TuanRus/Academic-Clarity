import { Role } from '../types/auth';

/**
 * Nhãn hiển thị cho Role trên UI.
 * RESEARCHER là khái niệm NỘI BỘ (user đăng ký bằng email cá nhân) - không hiển thị chữ
 * "Researcher"/"RESEARCHER" ra ngoài. Khi nâng cấp, mọi user (RESEARCHER hay EDU) đều được
 * gọi chung là "Premium" (xem accessTier ở src/types/auth.ts) - role chỉ dùng ngầm để xác
 * định mức giá ưu đãi (xem lib/pricing.ts) và các tính năng phù hợp cho người nghiên cứu.
 */
const ROLE_LABEL: Record<Role, string> = {
  [Role.RESEARCHER]: 'Cá nhân',
  [Role.EDU]: 'Học thuật (EDU)',
  [Role.ADMIN]: 'Quản trị viên',
};

export function getRoleLabel(role: Role): string {
  return ROLE_LABEL[role];
}

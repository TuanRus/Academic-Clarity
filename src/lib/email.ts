import { Role } from '../types/auth';

/**
 * Nhận diện email học thuật (.edu): áp dụng cho mọi domain có chứa ".edu"
 * (ví dụ: university.edu, university.edu.vn) - không phân biệt hoa/thường.
 */
export function isEduEmail(email: string): boolean {
  const domain = email.split('@')[1]?.toLowerCase() ?? '';
  return domain.includes('.edu');
}

/**
 * Tự suy ra Role từ email khi đăng ký (bỏ chọn role tay - xem RegisterPage):
 * - Email .edu -> EDU (gộp chung Lecturer + Student).
 * - Còn lại -> RESEARCHER.
 * Role = ADMIN chỉ được gán thủ công bởi quản trị viên (xem AdminDashboardPage), không qua Register.
 */
export function inferRoleFromEmail(email: string): Role {
  return isEduEmail(email) ? Role.EDU : Role.RESEARCHER;
}

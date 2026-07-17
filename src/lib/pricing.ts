import { Role, type User } from '../types/auth';

/**
 * Giá Premium (VNĐ/tháng). Ưu đãi cho user role = EDU (đăng ký bằng mail .edu - xem lib/email.ts):
 * 49.000đ thay vì giá chuẩn 99.000đ. Admin không cần thanh toán (BR-26, xem hasFeature()).
 */
export const PREMIUM_MONTHLY_PRICE = {
  standard: 99_000,
  edu: 49_000,
} as const;

/** Tỉ lệ tiết kiệm khi mua gói năm so với 12 tháng lẻ (giống mức hiển thị cũ ~16%). */
const YEARLY_DISCOUNT_RATIO = 0.84;

export function getPremiumMonthlyPrice(user: User | null): number {
  return user?.role === Role.EDU ? PREMIUM_MONTHLY_PRICE.edu : PREMIUM_MONTHLY_PRICE.standard;
}

export function getPremiumYearlyPrice(user: User | null): number {
  const monthly = getPremiumMonthlyPrice(user);
  return Math.round((monthly * 12 * YEARLY_DISCOUNT_RATIO) / 1000) * 1000;
}

export function formatVnd(amount: number): string {
  return `${amount.toLocaleString('vi-VN')}₫`;
}

import { apiPost, apiGet } from '../http';

export interface PublicPlan {
  planId: number;
  planName: string;
  priceAmount: number;
  durationDays: number;
}

/** GET /api/subscriptions/plans — danh sách gói Premium đang mở bán. */
export function getPublicPlans(): Promise<PublicPlan[]> {
  return apiGet<PublicPlan[]>('/subscriptions/plans');
}

// Tầng service cho luồng thanh toán Premium qua cổng PayOS (BR-26..31).

export interface PaymentLinkResponse {
  paymentUrl: string;
  qrCode: string;
  finalAmount: number;
}

/** POST /api/payments/create-link — khởi tạo phiên thanh toán PayOS, trả link checkout để redirect. */
export function createPaymentLink(planId: number): Promise<PaymentLinkResponse> {
  return apiPost<PaymentLinkResponse>('/payments/create-link', { planId });
}

/** GET /api/payments/verify/{orderCode} — xác nhận thanh toán khi quay về ReturnUrl (không cần webhook). */
export function verifyPayment(orderCode: string | number): Promise<{ upgraded: boolean }> {
  return apiGet<{ upgraded: boolean }>(`/payments/verify/${orderCode}`);
}

import { apiGet, apiPut } from '../http';

// Tầng service cho thông báo phía user — nối MyNotificationController (BE).
export interface NotificationItem {
  notificationId: number;
  title: string;
  message: string;
  relatedPaperId: string | null;
  isRead: boolean;
  createdAt: string;
}

/** GET /api/notifications/me — danh sách thông báo của user. */
export function getMyNotifications(limit = 30): Promise<NotificationItem[]> {
  return apiGet<NotificationItem[]>('/notifications/me', { limit });
}

/** GET /api/notifications/me/unread-count — số chưa đọc (badge chuông). */
export function getUnreadCount(): Promise<{ count: number }> {
  return apiGet<{ count: number }>('/notifications/me/unread-count');
}

/** PUT /api/notifications/me/{id}/read — đánh dấu 1 thông báo đã đọc. */
export function markRead(id: number): Promise<unknown> {
  return apiPut(`/notifications/me/${id}/read`);
}

/** PUT /api/notifications/me/read-all — đánh dấu tất cả đã đọc. */
export function markAllRead(): Promise<unknown> {
  return apiPut('/notifications/me/read-all');
}

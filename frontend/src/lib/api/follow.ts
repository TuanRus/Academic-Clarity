import { apiGet, apiPost } from '../http';

// Tầng service cho Follow (topic/journal) — nối FollowController (BE).
export interface FollowedItem {
  followId: number;
  targetType: string; // 'topic' | 'journal'
  targetId: string;
  name: string;
}
export interface FollowResult {
  isFollowing: boolean;
  totalFollowers: number;
}

/** POST /api/follows/toggle — bật/tắt theo dõi topic hoặc journal. */
export function toggleFollow(targetType: 'topic' | 'journal', targetId: string): Promise<FollowResult> {
  return apiPost<FollowResult>('/follows/toggle', { targetType, targetId });
}

/** GET /api/follows — danh sách mục đang theo dõi của user. */
export function getMyFollows(): Promise<FollowedItem[]> {
  return apiGet<FollowedItem[]>('/follows');
}

import { apiGet, apiPost } from '../http';

// Tầng service cho Follow (topic/journal/author) — nối FollowController (BE).
export type FollowTargetType = 'topic' | 'journal' | 'author';
export interface FollowedItem {
  followId: number;
  targetType: string; // 'topic' | 'journal' | 'author'
  targetId: string;
  name: string;
}
export interface FollowResult {
  isFollowing: boolean;
  totalFollowers: number;
}

/** POST /api/follows/toggle — bật/tắt theo dõi topic / journal / author. */
export function toggleFollow(targetType: FollowTargetType, targetId: string): Promise<FollowResult> {
  return apiPost<FollowResult>('/follows/toggle', { targetType, targetId });
}

/** GET /api/follows — danh sách mục đang theo dõi của user. */
export function getMyFollows(): Promise<FollowedItem[]> {
  return apiGet<FollowedItem[]>('/follows');
}

import { apiGet, apiPost, apiDelete } from '../http';

// Tầng service cho lịch sử tìm kiếm — nối SearchHistoryController (BE) /api/search/history.
export interface SearchHistoryItem {
  searchHistoryId: number;
  searchText: string;
  searchType: string;
  searchedAt: string;
}

/** GET /api/search/history — lịch sử tìm kiếm của user (mới nhất trước). */
export function getHistory(limit = 8): Promise<SearchHistoryItem[]> {
  return apiGet<SearchHistoryItem[]>('/search/history', { limit });
}

/** POST /api/search/history — lưu 1 lượt tìm kiếm. */
export function saveHistory(searchText: string, searchType = 'keyword'): Promise<unknown> {
  return apiPost('/search/history', { searchText, searchType });
}

/** DELETE /api/search/history — xóa toàn bộ lịch sử. */
export function clearHistory(): Promise<unknown> {
  return apiDelete('/search/history');
}

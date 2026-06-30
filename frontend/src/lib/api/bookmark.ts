import { apiGet, apiPost, apiDelete } from '../http';

// Tầng service cho Bookmark — nối BookmarkController (BE) /api/bookmarks.
export interface BookmarkResponse {
  bookmarkId: number;
  targetType: string;
  paperId: string | null;
  keywordId: string | null;
  createdAt: string;
}

/** GET /api/bookmarks — danh sách bookmark của user đang đăng nhập. */
export function getMyBookmarks(): Promise<BookmarkResponse[]> {
  return apiGet<BookmarkResponse[]>('/bookmarks');
}

/** POST /api/bookmarks — thêm bookmark cho 1 paper. */
export function addPaperBookmark(paperId: string): Promise<unknown> {
  return apiPost('/bookmarks', { targetType: 'paper', paperId });
}

/** DELETE /api/bookmarks/{id} — xóa bookmark theo bookmarkId. */
export function removeBookmark(bookmarkId: number): Promise<unknown> {
  return apiDelete(`/bookmarks/${bookmarkId}`);
}

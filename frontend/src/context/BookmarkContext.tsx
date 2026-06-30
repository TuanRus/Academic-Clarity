import { createContext, useState, useEffect, useCallback, type ReactNode } from 'react';
import { getAuthToken } from '../lib/http';
import { getMyBookmarks, addPaperBookmark, removeBookmark } from '../lib/api/bookmark';

export interface BookmarkedPaper {
  paperId: string;
  title: string;
  year: number | null;
  keywords: string[];
  citationCount?: number;
  sourceUrl?: string;
}

interface BookmarkContextValue {
  bookmarkedPapers: BookmarkedPaper[];
  isBookmarked: (paperId: string) => boolean;
  /**
   * BR-32: Bookmark và Followed Item (paper_id) tạo/xóa ĐỒNG THỜI, tỉ lệ 1-1 (user x paper).
   * Đã nối Backend (/api/bookmarks) — persist xuống DB, không mất khi reload.
   * Metadata bài (title/year/keywords) cache ở localStorage để trang Library hiển thị đầy đủ.
   */
  toggleBookmark: (paper: BookmarkedPaper) => void;
}

export const BookmarkContext = createContext<BookmarkContextValue | undefined>(undefined);

// Cache metadata để Library hiển thị title/year/keywords (BE chỉ trả paperId).
const META_KEY = 'stt_bookmark_meta';
type MetaMap = Record<string, BookmarkedPaper>;

function loadMeta(): MetaMap {
  try {
    return JSON.parse(localStorage.getItem(META_KEY) || '{}') as MetaMap;
  } catch {
    return {};
  }
}
function saveMeta(meta: MetaMap) {
  localStorage.setItem(META_KEY, JSON.stringify(meta));
}

export function BookmarkProvider({ children }: { children: ReactNode }) {
  const [bookmarkedPapers, setBookmarkedPapers] = useState<BookmarkedPaper[]>([]);
  // paperId -> bookmarkId (để xóa qua DELETE /api/bookmarks/{id}).
  const [idMap, setIdMap] = useState<Record<string, number>>({});

  const refresh = useCallback(async () => {
    if (!getAuthToken()) {
      setBookmarkedPapers([]);
      setIdMap({});
      return;
    }
    try {
      const rows = await getMyBookmarks();
      const meta = loadMeta();
      const map: Record<string, number> = {};
      const papers: BookmarkedPaper[] = [];
      for (const r of rows) {
        if (r.targetType !== 'paper' || !r.paperId) continue;
        map[r.paperId] = r.bookmarkId;
        papers.push(
          meta[r.paperId] ?? { paperId: r.paperId, title: r.paperId, year: null, keywords: [] }
        );
      }
      setIdMap(map);
      setBookmarkedPapers(papers);
    } catch {
      // 401/lỗi mạng → để trống, không chặn UI.
    }
  }, []);

  useEffect(() => {
    void refresh();
    // Re-fetch khi đăng nhập/đăng xuất (AuthContext phát event này).
    const onAuthChange = () => void refresh();
    window.addEventListener('stt-auth-changed', onAuthChange);
    return () => window.removeEventListener('stt-auth-changed', onAuthChange);
  }, [refresh]);

  const isBookmarked = (paperId: string) => bookmarkedPapers.some((p) => p.paperId === paperId);

  const toggleBookmark = (paper: BookmarkedPaper) => {
    const existing = bookmarkedPapers.some((p) => p.paperId === paper.paperId);
    const meta = loadMeta();

    if (existing) {
      // Optimistic remove
      setBookmarkedPapers((prev) => prev.filter((p) => p.paperId !== paper.paperId));
      delete meta[paper.paperId];
      saveMeta(meta);
      const bid = idMap[paper.paperId];
      if (bid != null) {
        removeBookmark(bid)
          .then(() => refresh())
          .catch(() => refresh());
      }
    } else {
      // Optimistic add
      setBookmarkedPapers((prev) => [...prev, paper]);
      meta[paper.paperId] = paper;
      saveMeta(meta);
      addPaperBookmark(paper.paperId)
        .then(() => refresh())
        .catch(() => refresh());
    }
  };

  return (
    <BookmarkContext.Provider value={{ bookmarkedPapers, isBookmarked, toggleBookmark }}>
      {children}
    </BookmarkContext.Provider>
  );
}

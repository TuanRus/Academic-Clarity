import { createContext, useState, type ReactNode } from 'react';

export interface BookmarkedPaper {
  paperId: string;
  title: string;
  authors: string[];
  journal: string;
  year: number;
  keywords: string[];
}

interface BookmarkContextValue {
  bookmarkedPapers: BookmarkedPaper[];
  isBookmarked: (paperId: string) => boolean;
  /**
   * BR-32: Bookmark và Followed Item (paper_id) luôn được tạo/xóa ĐỒNG THỜI,
   * trong cùng 1 "transaction", tỉ lệ 1-1 (user x paper).
   * Chỉ MỘT hành động duy nhất ("Bookmark Paper" / "Remove Bookmark") -
   * không có UI riêng cho "follow paper".
   */
  toggleBookmark: (paper: BookmarkedPaper) => void;
}

export const BookmarkContext = createContext<BookmarkContextValue | undefined>(undefined);

export function BookmarkProvider({ children }: { children: ReactNode }) {
  const [bookmarkedPapers, setBookmarkedPapers] = useState<BookmarkedPaper[]>([]);

  const isBookmarked = (paperId: string) => bookmarkedPapers.some((p) => p.paperId === paperId);

  const toggleBookmark = (paper: BookmarkedPaper) => {
    setBookmarkedPapers((prev) =>
      prev.some((p) => p.paperId === paper.paperId)
        ? prev.filter((p) => p.paperId !== paper.paperId) // FOLLOWED -> NONE: xóa Bookmark + Followed Item
        : [...prev, paper] // NONE -> FOLLOWED: tạo Bookmark + Followed Item
    );
  };

  return (
    <BookmarkContext.Provider value={{ bookmarkedPapers, isBookmarked, toggleBookmark }}>
      {children}
    </BookmarkContext.Provider>
  );
}

import { useContext } from 'react';
import { BookmarkContext } from '../context/BookmarkContext';

export function useBookmark() {
  const ctx = useContext(BookmarkContext);
  if (!ctx) throw new Error('useBookmark must be used within BookmarkProvider');
  return ctx;
}

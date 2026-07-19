import { apiGet } from '../http';
import type { MindmapGraph, PagedResult, PaperDetail, PaperSearchItem } from '../../types/api';

// Tầng service cho controller api/mindmap. 1 hàm = 1 endpoint backend.

/** GET /api/mindmap/keywords/suggest — autocomplete keyword có sẵn trong DB. */
export function suggestKeywords(q: string, limit = 10): Promise<string[]> {
  return apiGet<string[]>('/mindmap/keywords/suggest', { q, limit });
}

/** GET /api/mindmap/authors/suggest — autocomplete tên tác giả có trong DB. */
export function suggestAuthors(q: string, limit = 10): Promise<string[]> {
  return apiGet<string[]>('/mindmap/authors/suggest', { q, limit });
}

/** GET /api/mindmap/search — tìm bài báo theo từ khóa (tiêu đề/DOI). */
export function searchPapers(
  q: string,
  page = 1,
  pageSize = 20,
  opts?: { fromYear?: number; toYear?: number }
): Promise<PagedResult<PaperSearchItem>> {
  return apiGet<PagedResult<PaperSearchItem>>('/mindmap/search', {
    q,
    page,
    pageSize,
    ...(opts?.fromYear != null ? { fromYear: opts.fromYear } : {}),
    ...(opts?.toYear != null ? { toYear: opts.toYear } : {}),
  });
}

/** GET /api/mindmap/search/author — tìm bài báo theo tên tác giả. */
export function searchPapersByAuthor(author: string, page = 1, pageSize = 20): Promise<PagedResult<PaperSearchItem>> {
  return apiGet<PagedResult<PaperSearchItem>>('/mindmap/search/author', { author, page, pageSize });
}

/** GET /api/mindmap/search/journal — tìm bài báo theo tên tạp chí. */
export function searchPapersByJournal(journal: string, page = 1, pageSize = 20): Promise<PagedResult<PaperSearchItem>> {
  return apiGet<PagedResult<PaperSearchItem>>('/mindmap/search/journal', { journal, page, pageSize });
}

/** GET /api/mindmap/paper/{paperId} — chi tiết đầy đủ 1 bài báo (kèm abstract từ OpenAlex). */
export function getPaperDetail(paperId: string): Promise<PaperDetail> {
  return apiGet<PaperDetail>(`/mindmap/paper/${encodeURIComponent(paperId)}`);
}

/** GET /api/mindmap/tree/keyword — cây mind map 3 tầng quanh 1 keyword. */
export function getKeywordTree(keyword: string, maxBranches = 6, maxSubBranches = 3): Promise<MindmapGraph> {
  return apiGet<MindmapGraph>('/mindmap/tree/keyword', { keyword, maxBranches, maxSubBranches });
}

/** GET /api/mindmap/papers/keyword — top bài báo của 1 keyword (panel chi tiết). */
export function getTopPapersByKeyword(keyword: string, distinctFrom?: string, limit = 10): Promise<PaperSearchItem[]> {
  return apiGet<PaperSearchItem[]>('/mindmap/papers/keyword', { keyword, distinctFrom, limit });
}

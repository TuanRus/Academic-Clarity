import { apiGet } from '../http';
import type { TrendDimension, TrendSeries, TrendTopItem } from '../../types/api';

// Tầng service cho controller api/trend.
const CURRENT_YEAR = new Date().getFullYear(); // năm hiện tại, không hardcode

/** GET /api/trend/series — time-series share của 1 entity (keyword/author/journal). */
export function getTrendSeries(
  dimension: TrendDimension,
  value: string,
  opts: { fromYear?: number; toYear?: number; groupBy?: 'year' | 'month' } = {},
): Promise<TrendSeries> {
  const { fromYear = 2022, toYear = CURRENT_YEAR, groupBy = 'year' } = opts;
  return apiGet<TrendSeries>('/trend/series', { dimension, value, fromYear, toYear, groupBy });
}

/** GET /api/trend/top — bảng xếp hạng top entity theo share/rising/falling. */
export function getTrendTop(
  dimension: TrendDimension,
  opts: { fromYear?: number; toYear?: number; topN?: number; minPapers?: number; sortBy?: 'share' | 'rising' | 'falling' } = {},
): Promise<TrendTopItem[]> {
  const { fromYear = 2022, toYear = CURRENT_YEAR, topN = 20, minPapers = 3, sortBy = 'share' } = opts;
  return apiGet<TrendTopItem[]>('/trend/top', { dimension, fromYear, toYear, topN, minPapers, sortBy });
}

// --- PR #2: drill-down theo keyword (Premium → nhiều kết quả, Basic → 2) ---
export interface TrendPremiumPaper {
  paperId: string;
  title: string;
  publicationYear: number | null;
  citationCount: number;
  sourceUrl: string;
  journalName: string;
}
export interface TrendPremiumAuthor {
  authorId: number;
  fullName: string;
  paperCount: number;
}
export interface TrendPremiumJournal {
  journalId: string;
  journalName: string;
  paperCount: number;
}
export interface CoOccurringKeyword {
  keywordName: string;
  coOccurrenceCount: number;
}

type YearOpts = { fromYear?: number; toYear?: number };
const yq = (opts: YearOpts = {}) => ({ fromYear: opts.fromYear ?? 2022, toYear: opts.toYear ?? CURRENT_YEAR });

/** GET /api/trend/keyword/papers — top bài báo theo keyword. */
export function getKeywordTopPapers(keyword: string, opts?: YearOpts): Promise<TrendPremiumPaper[]> {
  return apiGet<TrendPremiumPaper[]>('/trend/keyword/papers', { keyword, ...yq(opts) });
}
/** GET /api/trend/keyword/authors — top tác giả theo keyword. */
export function getKeywordTopAuthors(keyword: string, opts?: YearOpts): Promise<TrendPremiumAuthor[]> {
  return apiGet<TrendPremiumAuthor[]>('/trend/keyword/authors', { keyword, ...yq(opts) });
}
/** GET /api/trend/keyword/journals — top tạp chí theo keyword. */
export function getKeywordTopJournals(keyword: string, opts?: YearOpts): Promise<TrendPremiumJournal[]> {
  return apiGet<TrendPremiumJournal[]>('/trend/keyword/journals', { keyword, ...yq(opts) });
}
/** GET /api/trend/keyword/co-occurring — keyword đồng xuất hiện. */
export function getCoOccurringKeywords(keyword: string, opts?: YearOpts): Promise<CoOccurringKeyword[]> {
  return apiGet<CoOccurringKeyword[]>('/trend/keyword/co-occurring', { keyword, ...yq(opts) });
}

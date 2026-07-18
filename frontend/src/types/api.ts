// Các kiểu dữ liệu khớp với DTO của backend (ScientificTrendTracker).
// Backend bọc mọi response trong ApiResponse<T>; http client sẽ tự bóc tách phần Data.

export interface ApiResponse<T> {
  success: boolean;
  statusCode: number;
  message: string;
  data: T;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// --- Mindmap (api/mindmap) ---

export interface PaperSearchItem {
  paperId: string;
  title: string;
  year: number | null;
  citationCount: number;
  journalName: string;
  quartile: string;
  sourceUrl: string;
  keywordCount: number;
}

export interface MindmapNode {
  id: string;
  type: string; // "keyword" | "paper"
  label: string;
  level?: number; // 0 = tâm, 1 = chủ đề con, 2 = chủ đề cháu
  paperCount?: number;
  trendScore?: number;
  year?: number;
  citationCount?: number;
  quartile?: string;
  sourceUrl?: string;
}

export interface MindmapEdge {
  source: string;
  target: string;
}

export interface MindmapGraph {
  searchQuery: string;
  totalNodes: number;
  totalEdges: number;
  nodes: MindmapNode[];
  edges: MindmapEdge[];
}

export interface PaperDetail {
  paperId: string;
  openAlexId: string | null;
  doi: string | null;
  title: string;
  publicationYear: number | null;
  publicationDate: string | null;
  citationCount: number;
  sourceUrl: string | null;
  journalName: string | null;
  quartile: string | null;
  publisher: string | null;
  impactFactor: number | null;
  authors: string[];
  keywords: string[];
  // Topic lưu DB; subfield/field/domain/openAccessStatus/institutions lấy on-demand từ OpenAlex.
  topic: string | null;
  subfield: string | null;
  field: string | null;
  domain: string | null;
  openAccessStatus: string | null;
  institutions: string[];
  abstract: string | null;
}

// --- Trend (api/trend) ---

export type TrendDimension = 'keyword' | 'author' | 'journal';
export type TrendDirection = 'rising' | 'falling' | 'stable';

export interface TrendSeriesPoint {
  period: string; // "2024" hoặc "2024-03"
  count: number;
  periodTotal: number;
  share: number;
}

export interface TrendSeries {
  dimension: TrendDimension;
  value: string;
  groupBy: string;
  totalPapers: number;
  series: TrendSeriesPoint[];
  slope: number;
  direction: TrendDirection;
}

export interface TrendTopItem {
  name: string;
  count: number;
  rangeTotal: number;
  share: number;
  slope: number;
  direction: TrendDirection;
}

// --- Idea Overlap Checker (api/idea) ---

export type OverlapTier = 'high' | 'medium' | 'low';

export interface OverlapMatch {
  paperId: string;
  title: string;
  year: number | null;
  citationCount: number;
  journalName: string | null;
  sourceUrl: string | null;
  sharedKeywords: string[];
  score: number; // 0..1
  tier: OverlapTier;
  aiNote?: string | null; // nhận định AI: bài này trùng ý tưởng ở điểm nào
}

export interface OverlapResult {
  extractedKeywords: string[];
  matchedKeywordCount: number;
  matches: OverlapMatch[];
  aiRisk?: OverlapTier | null;    // mức rủi ro trùng ý tưởng do AI đánh giá
  aiAssessment?: string | null;   // nhận định tổng hợp của AI
  finalVerdict?: OverlapTier | null; // kết luận cuối = max(keyword tier, aiRisk)
}

// --- LaTeX Writer (api/latex) ---

export interface Citation {
  bibtexKey: string; // key dùng trong \cite{...}
  bibtex: string;    // entry @article{...} đầy đủ để copy
  bibitem: string;   // dòng \bibitem{...} chèn vào thebibliography
}

export interface LatexCompileResult {
  pdf: string | null; // PDF base64 khi compile thành công
  log: string | null; // log TeX khi thất bại
}

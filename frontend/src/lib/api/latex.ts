import { apiGet, apiPost } from '../http';
import type { Citation, LatexCompileResult } from '../../types/api';

// Tầng service cho controller api/latex (LaTeX Writer - premium).
// Tài liệu LaTeX lưu PHÍA CLIENT (xem lib/latexStorage.ts) — backend không lưu gì:
// chỉ sinh citation read-only từ corpus và proxy compile PDF qua texlive.net.

/**
 * GET /api/latex/citation/{paperId} — sinh BibTeX + \bibitem từ paper trong corpus
 * để chèn citation vào tài liệu đang soạn.
 */
export function getCitation(paperId: string): Promise<Citation> {
  return apiGet<Citation>(`/latex/citation/${encodeURIComponent(paperId)}`);
}

/**
 * POST /api/latex/compile — compile nguồn .tex → { pdf: base64 | null, log }.
 * Backend giới hạn 1 lần/10s mỗi user (đi qua dịch vụ công cộng texlive.net).
 */
export function compileLatex(content: string): Promise<LatexCompileResult> {
  return apiPost<LatexCompileResult>('/latex/compile', { content });
}

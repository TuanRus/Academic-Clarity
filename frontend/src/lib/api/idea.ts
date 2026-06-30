import { apiPost } from '../http';
import type { OverlapResult } from '../../types/api';

// Tầng service cho controller api/idea (Idea Overlap Checker - premium).

/**
 * POST /api/idea/check-overlap — dán abstract → trích keyword → so corpus → cảnh báo sớm trùng lặp.
 * Abstract xử lý in-memory phía backend, KHÔNG lưu DB.
 */
export function checkOverlap(abstractText: string): Promise<OverlapResult> {
  return apiPost<OverlapResult>('/idea/check-overlap', { abstract: abstractText });
}

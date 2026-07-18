import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import type { OverlapMatch, OverlapResult, OverlapTier } from '../../types/api';

// Khối hiển thị kết quả Idea Overlap Check — dùng chung cho OverlapCheckerPage
// và tab Overlap Check của LaTeX Writer (tab này truyền matchAction để render nút Cite).

export const tierStyle: Record<OverlapTier, { label: string; badge: string; bar: string }> = {
  high: { label: 'High overlap', badge: 'bg-red-50 text-red-700 ring-red-200', bar: 'bg-red-500' },
  medium: { label: 'Moderate overlap', badge: 'bg-amber-50 text-amber-700 ring-amber-200', bar: 'bg-amber-500' },
  low: { label: 'Low overlap', badge: 'bg-gray-100 text-gray-600 ring-gray-200', bar: 'bg-gray-400' },
};

interface OverlapResultListProps {
  result: OverlapResult;
  /** Render thêm hành động cho từng bài trùng (vd: nút "Cite" trong LaTeX Writer). */
  matchAction?: (match: OverlapMatch) => ReactNode;
}

const OverlapResultList = ({ result, matchAction }: OverlapResultListProps) => {
  return (
    <div className="space-y-4">
      {/* AI idea-overlap verdict (kết hợp keyword + phân tích ngữ nghĩa) */}
      {(result.aiAssessment || result.finalVerdict || result.aiRisk) && (() => {
        const verdict = (result.finalVerdict ?? result.aiRisk ?? 'low') as OverlapTier;
        const vs = tierStyle[verdict];
        return (
          <div className={`rounded-lg p-3 ring-1 ${vs.badge}`}>
            <div className="flex items-center gap-2">
              <span className="text-xs font-bold uppercase tracking-wide">AI idea-overlap verdict</span>
              <span className={`rounded-full px-2 py-0.5 text-[11px] font-bold ring-1 ${vs.badge}`}>
                {verdict.toUpperCase()}
              </span>
            </div>
            {result.aiAssessment ? (
              <p className="mt-1.5 text-sm">{result.aiAssessment}</p>
            ) : (
              <p className="mt-1.5 text-xs opacity-70">Combined from keyword evidence (AI narrative unavailable).</p>
            )}
          </div>
        );
      })()}

      {/* Extracted keywords */}
      <div>
        <p className="mb-1.5 text-xs font-medium uppercase tracking-wide text-gray-400">
          Keywords extracted from your abstract
        </p>
        {result.extractedKeywords.length === 0 ? (
          <p className="text-sm text-gray-400">No keywords extracted (check the AI service).</p>
        ) : (
          <div className="flex flex-wrap gap-1.5">
            {result.extractedKeywords.map((k) => (
              <span key={k} className="rounded-full bg-indigo-50 px-2.5 py-0.5 text-xs font-medium text-indigo-700">
                {k}
              </span>
            ))}
          </div>
        )}
      </div>

      {/* Matched papers */}
      {result.matches.length === 0 ? (
        <div className="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700">
          No papers share significant keywords — your idea looks novel.
        </div>
      ) : (
        <ul className="space-y-3">
          {result.matches.map((m) => {
            const ts = tierStyle[m.tier];
            return (
              <li key={m.paperId} className="rounded-lg border border-gray-100 p-3">
                <div className="flex items-start justify-between gap-2">
                  <Link
                    to={`/papers/${encodeURIComponent(m.paperId)}`}
                    className="text-sm font-medium text-indigo-700 hover:underline"
                  >
                    {m.title}
                  </Link>
                  <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ring-1 ${ts.badge}`}>
                    {ts.label}
                  </span>
                </div>
                <p className="mt-0.5 text-xs text-gray-500">
                  {m.year ?? '—'} · {m.citationCount} citations
                  {m.journalName ? ` · ${m.journalName}` : ''}
                </p>
                {/* Overlap score bar */}
                <div className="mt-2 flex items-center gap-2">
                  <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-gray-100">
                    <div
                      className={`h-full ${ts.bar}`}
                      style={{ width: `${Math.min(100, Math.round(m.score * 100))}%` }}
                    />
                  </div>
                  <span className="w-10 text-right text-xs text-gray-500">
                    {Math.round(m.score * 100)}%
                  </span>
                </div>
                {/* Shared keywords */}
                <div className="mt-2 flex flex-wrap gap-1">
                  {m.sharedKeywords.map((k) => (
                    <span key={k} className="rounded bg-gray-100 px-1.5 py-0.5 text-[11px] text-gray-600">
                      {k}
                    </span>
                  ))}
                </div>
                {/* Nhận định AI cho riêng bài này */}
                {m.aiNote && (
                  <p className="mt-2 rounded bg-indigo-50 px-2 py-1 text-xs text-indigo-800">
                    <span className="font-semibold">AI: </span>{m.aiNote}
                  </p>
                )}
                {matchAction && <div className="mt-2">{matchAction(m)}</div>}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
};

export default OverlapResultList;

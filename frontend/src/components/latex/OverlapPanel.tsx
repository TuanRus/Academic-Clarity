import { useRef, useState } from 'react';
import OverlapResultList from '../overlap/OverlapResultList';
import { checkOverlap } from '../../lib/api/idea';
import { getCitation } from '../../lib/api/latex';
import { ApiError } from '../../lib/http';
import type { Citation, OverlapMatch, OverlapResult } from '../../types/api';

// Tab "Overlap Check" trong LaTeX Writer: trích abstract từ bản nháp → gọi Idea Overlap
// Checker sẵn có → mỗi bài trùng có nút Cite (chèn \cite + \bibitem) và Copy BibTeX.

const MIN_LEN = 80;
const MAX_LEN = 6000;

/** Bỏ lệnh LaTeX/comment để lấy văn bản thô gửi cho overlap checker. */
function stripLatex(src: string): string {
  return src
    .replace(/(?<!\\)%.*$/gm, ' ')                  // comment (giữ \% literal)
    .replace(/\\[a-zA-Z]+\*?(\[[^\]]*\])?/g, ' ')   // \command[opt] — giữ nội dung trong {}
    .replace(/[{}~]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

/** Ưu tiên đoạn user bôi đen; không có thì lấy nội dung \begin{abstract}…\end{abstract}. */
function extractDraftText(full: string, selection: string): { text?: string; error?: string } {
  const sel = stripLatex(selection);
  if (sel.length >= MIN_LEN) return { text: sel.slice(0, MAX_LEN) };

  const m = full.match(/\\begin\{abstract\}([\s\S]*?)\\end\{abstract\}/);
  if (!m) {
    return {
      error:
        'No \\begin{abstract}…\\end{abstract} found. Write your abstract in the template, or select a passage (≥ 80 characters) in the editor.',
    };
  }
  const abs = stripLatex(m[1]);
  if (abs.length < MIN_LEN)
    return { error: `Your abstract is too short (${abs.length} chars) — write at least ${MIN_LEN} characters.` };
  return { text: abs.slice(0, MAX_LEN) };
}

interface OverlapPanelProps {
  /** Lấy nội dung hiện tại của editor: toàn văn + đoạn đang bôi đen. */
  getDraft: () => { full: string; selection: string };
  /** Chèn citation vào editor (parent xử lý vị trí cursor + thebibliography). */
  onCite: (citation: Citation) => void;
}

const OverlapPanel = ({ getDraft, onCite }: OverlapPanelProps) => {
  const [result, setResult] = useState<OverlapResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [citedKey, setCitedKey] = useState<string | null>(null);
  const [copiedFor, setCopiedFor] = useState<string | null>(null);
  // Cache citation theo paperId để "Cite" rồi "Copy BibTeX" không gọi API 2 lần.
  const citationCache = useRef(new Map<string, Citation>());

  const run = () => {
    const { full, selection } = getDraft();
    const extracted = extractDraftText(full, selection);
    if (!extracted.text) {
      setError(extracted.error ?? 'Could not extract text.');
      setResult(null);
      return;
    }
    setLoading(true);
    setError(null);
    setResult(null);
    checkOverlap(extracted.text)
      .then(setResult)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'Could not analyze right now.'))
      .finally(() => setLoading(false));
  };

  const fetchCitation = (m: OverlapMatch): Promise<Citation> => {
    const cached = citationCache.current.get(m.paperId);
    if (cached) return Promise.resolve(cached);
    return getCitation(m.paperId).then((c) => {
      citationCache.current.set(m.paperId, c);
      return c;
    });
  };

  const handleCite = (m: OverlapMatch) => {
    fetchCitation(m)
      .then((c) => {
        onCite(c);
        setCitedKey(m.paperId);
        window.setTimeout(() => setCitedKey((k) => (k === m.paperId ? null : k)), 2000);
      })
      .catch((e) => setError(e instanceof ApiError ? e.message : 'Could not generate the citation.'));
  };

  const handleCopyBibtex = (m: OverlapMatch) => {
    fetchCitation(m)
      .then((c) => navigator.clipboard.writeText(c.bibtex))
      .then(() => {
        setCopiedFor(m.paperId);
        window.setTimeout(() => setCopiedFor((k) => (k === m.paperId ? null : k)), 2000);
      })
      .catch((e) => setError(e instanceof ApiError ? e.message : 'Could not copy the BibTeX entry.'));
  };

  return (
    <div className="flex h-full flex-col">
      <div className="flex items-center justify-between gap-2 border-b border-gray-100 px-4 py-2">
        <span className="text-xs text-gray-500">
          Analyzes your <code className="text-gray-600">abstract</code> (or the selected text) against the corpus.
        </span>
        <button
          type="button"
          onClick={run}
          disabled={loading}
          className="shrink-0 rounded-md bg-indigo-700 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-800 disabled:cursor-not-allowed disabled:opacity-40"
        >
          {loading ? 'Analyzing…' : 'Check overlap'}
        </button>
      </div>

      <div className="flex-1 overflow-y-auto px-4 py-3">
        {loading && <p className="text-sm text-gray-400">Extracting keywords & matching the corpus…</p>}
        {error && <p className="text-sm text-red-600">{error}</p>}
        {!loading && !error && !result && (
          <div className="flex h-full flex-col items-center justify-center text-center text-sm text-gray-400">
            Write your abstract, then click <strong className="mx-1 text-gray-500">Check overlap</strong> — matching
            papers can be cited into your draft with one click.
          </div>
        )}
        {!loading && result && (
          <OverlapResultList
            result={result}
            matchAction={(m) => (
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() => handleCite(m)}
                  className="rounded-md bg-indigo-700 px-2.5 py-1 text-xs font-medium text-white hover:bg-indigo-800"
                >
                  {citedKey === m.paperId ? '✓ Inserted' : 'Cite'}
                </button>
                <button
                  type="button"
                  onClick={() => handleCopyBibtex(m)}
                  className="rounded-md border border-gray-300 px-2.5 py-1 text-xs font-medium text-gray-700 hover:bg-gray-50"
                >
                  {copiedFor === m.paperId ? '✓ Copied' : 'Copy BibTeX'}
                </button>
              </div>
            )}
          />
        )}
      </div>
    </div>
  );
};

export default OverlapPanel;

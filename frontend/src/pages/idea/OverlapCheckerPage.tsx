import { useState } from 'react';
import { Link } from 'react-router-dom';
import RequireFeature from '../../routes/RequireFeature';
import { FeaturePermission } from '../../types/permissions';
import { checkOverlap } from '../../lib/api/idea';
import { ApiError } from '../../lib/http';
import type { OverlapResult, OverlapTier } from '../../types/api';

const MAX_LEN = 6000;
const MIN_LEN = 80;

const tierStyle: Record<OverlapTier, { label: string; badge: string; bar: string }> = {
  high: { label: 'High overlap', badge: 'bg-red-50 text-red-700 ring-red-200', bar: 'bg-red-500' },
  medium: { label: 'Moderate overlap', badge: 'bg-amber-50 text-amber-700 ring-amber-200', bar: 'bg-amber-500' },
  low: { label: 'Low overlap', badge: 'bg-gray-100 text-gray-600 ring-gray-200', bar: 'bg-gray-400' },
};

const OverlapCheckerPage = () => {
  const [text, setText] = useState('');
  const [result, setResult] = useState<OverlapResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const trimmedLen = text.trim().length;
  const canSubmit = trimmedLen >= MIN_LEN && trimmedLen <= MAX_LEN && !loading;

  const run = () => {
    if (!canSubmit) return;
    setLoading(true);
    setError(null);
    setResult(null);
    checkOverlap(text.trim())
      .then(setResult)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'Could not analyze right now.'))
      .finally(() => setLoading(false));
  };

  return (
    <div className="space-y-5">
      {/* Title */}
      <div>
        <p className="text-xs font-medium uppercase tracking-wide text-gray-400">Premium · Research Assistant</p>
        <h1 className="text-2xl font-bold text-gray-900">Idea Overlap Checker</h1>
        <p className="mt-1 max-w-3xl text-sm text-gray-500">
          Paste your idea's abstract — we extract its keywords and compare them against our paper corpus to{' '}
          <strong className="text-gray-700">flag early</strong> any existing research on similar topics,
          helping you position your contribution before you write.
        </p>
      </div>

      {/* Disclaimer: this is NOT a plagiarism tool */}
      <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
        <p>
          This is an <strong>early-warning / preliminary screening</strong> tool, not a definitive overlap verdict.
          Shared keywords don't always mean shared ideas — read the suggested papers to judge for yourself. Your
          abstract is <strong>never stored</strong>.
        </p>
      </div>

      {/* Premium gate: BASIC users see the UpgradeOverlay */}
      <RequireFeature
        feature={FeaturePermission.OVERLAP_CHECK}
        featureLabel="Idea Overlap Checker"
        description="Keyword-based idea overlap checking is available for Premium accounts only. Upgrade to use it and protect your idea."
      >
        {/* Google-Translate-style two panes */}
        <div className="grid gap-4 lg:grid-cols-2">
          {/* LEFT: paste abstract */}
          <div className="flex flex-col rounded-xl border border-gray-200 bg-white shadow-sm">
            <div className="flex items-center justify-between border-b border-gray-100 px-4 py-2">
              <span className="text-sm font-semibold text-gray-700">Your abstract</span>
            </div>
            <textarea
              value={text}
              onChange={(e) => setText(e.target.value.slice(0, MAX_LEN))}
              placeholder="Paste your abstract (English) here…"
              className="h-[46vh] min-h-[300px] w-full resize-none px-4 py-3 text-sm text-gray-800 outline-none"
            />
            <div className="flex items-center justify-between border-t border-gray-100 px-4 py-2">
              <span className={`text-xs ${trimmedLen > MAX_LEN ? 'text-red-500' : 'text-gray-400'}`}>
                {trimmedLen}/{MAX_LEN} chars
                {trimmedLen > 0 && trimmedLen < MIN_LEN && ` · need ≥ ${MIN_LEN}`}
              </span>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => {
                    setText('');
                    setResult(null);
                    setError(null);
                  }}
                  className="rounded-md px-3 py-1.5 text-sm text-gray-500 hover:bg-gray-100"
                >
                  Clear
                </button>
                <button
                  type="button"
                  onClick={run}
                  disabled={!canSubmit}
                  className="rounded-md bg-indigo-700 px-4 py-1.5 text-sm font-medium text-white hover:bg-indigo-800 disabled:cursor-not-allowed disabled:opacity-40"
                >
                  {loading ? 'Analyzing…' : 'Check overlap'}
                </button>
              </div>
            </div>
          </div>

          {/* RIGHT: results */}
          <div className="flex flex-col rounded-xl border border-gray-200 bg-white shadow-sm">
            <div className="flex items-center justify-between border-b border-gray-100 px-4 py-2">
              <span className="text-sm font-semibold text-gray-700">Overlap warnings</span>
              {result && (
                <span className="text-xs text-gray-400">
                  {result.matchedKeywordCount}/{result.extractedKeywords.length} keywords in DB
                </span>
              )}
            </div>

            <div className="h-[46vh] min-h-[300px] overflow-y-auto px-4 py-3">
              {loading && <p className="text-sm text-gray-400">Extracting keywords & matching the corpus…</p>}
              {error && <p className="text-sm text-red-600">{error}</p>}

              {!loading && !error && !result && (
                <div className="flex h-full flex-col items-center justify-center text-center text-sm text-gray-400">
                  Paste an abstract on the left and click <strong className="mx-1 text-gray-500">Check overlap</strong> to
                  see similar research.
                </div>
              )}

              {!loading && !error && result && (
                <div className="space-y-4">
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
                          <span
                            key={k}
                            className="rounded-full bg-indigo-50 px-2.5 py-0.5 text-xs font-medium text-indigo-700"
                          >
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
                              <span
                                className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ring-1 ${ts.badge}`}
                              >
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
                                <span
                                  key={k}
                                  className="rounded bg-gray-100 px-1.5 py-0.5 text-[11px] text-gray-600"
                                >
                                  {k}
                                </span>
                              ))}
                            </div>
                          </li>
                        );
                      })}
                    </ul>
                  )}
                </div>
              )}
            </div>

            {/* Tier legend */}
            <div className="flex items-center gap-4 border-t border-gray-100 px-4 py-2 text-xs text-gray-500">
              <span className="flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-red-500" /> High ≥30%</span>
              <span className="flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-amber-500" /> Moderate 15–30%</span>
              <span className="flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-gray-400" /> Low &lt;15%</span>
            </div>
          </div>
        </div>

        {/* Extra: 3-step explainer — fills the screen + explains the method */}
        <div className="grid gap-3 sm:grid-cols-3">
          {[
            { n: '1', t: 'Extract keywords', d: 'A local AI reads your abstract and pulls out the core technical terms.' },
            { n: '2', t: 'Match the corpus', d: 'Compares keywords against the paper corpus, weighting rare (distinctive) terms higher than common ones.' },
            { n: '3', t: 'Early warning', d: 'Ranks similar papers by overlap so you can review them and position your contribution.' },
          ].map((s) => (
            <div key={s.n} className="rounded-xl border border-gray-200 bg-white p-4">
              <div className="mb-1 flex h-6 w-6 items-center justify-center rounded-full bg-indigo-100 text-xs font-bold text-indigo-700">
                {s.n}
              </div>
              <p className="text-sm font-semibold text-gray-800">{s.t}</p>
              <p className="mt-0.5 text-xs text-gray-500">{s.d}</p>
            </div>
          ))}
        </div>
      </RequireFeature>
    </div>
  );
};

export default OverlapCheckerPage;

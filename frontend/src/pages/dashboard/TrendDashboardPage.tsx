import { useEffect, useState } from 'react';
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import RequireFeature from '../../routes/RequireFeature';
import { useFeature } from '../../hooks/useFeature';
import { FeaturePermission } from '../../types/permissions';
import {
  getTrendSeries,
  getTrendTop,
  getKeywordTopPapers,
  getKeywordTopAuthors,
  getKeywordTopJournals,
  getCoOccurringKeywords,
  type TrendPremiumPaper,
  type TrendPremiumAuthor,
  type TrendPremiumJournal,
  type CoOccurringKeyword,
} from '../../lib/api/trend';
import { Link } from 'react-router-dom';
import { suggestKeywords } from '../../lib/api/mindmap';
import { getMyFollows, toggleFollow } from '../../lib/api/follow';
import { ApiError } from '../../lib/http';
import type { TrendSeries, TrendTopItem } from '../../types/api';

// Journal & Keywords · Trend Analytics Dashboard - FR-19..FR-22
// FR-19 (Line Chart cơ bản) -> DASHBOARD_BASIC.
// FR-20/21 (Stacked Bar Chart trending keywords + date filter nâng cao) -> DASHBOARD_ADVANCED.
// FR-22 (Export CSV) -> EXPORT_CSV (permission riêng, không gộp vào DASHBOARD_ADVANCED).
// ĐÃ NỐI API thật: bảng Top Trending Keywords (GET /api/trend/top).
const TrendDashboardPage = () => {
  const canExportCsv = useFeature(FeaturePermission.EXPORT_CSV);

  const [topKeywords, setTopKeywords] = useState<TrendTopItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Publication Volume Trend (line chart): /api/trend/series cần 1 keyword cụ thể
  // (backend không có "tổng toàn cục"). Ban đầu để trống -> hiện lời nhắc nhập keyword.
  const [seriesKeyword, setSeriesKeyword] = useState('');
  const [seriesInput, setSeriesInput] = useState('');
  const [seriesSuggestions, setSeriesSuggestions] = useState<string[]>([]);
  const [showSeriesSuggest, setShowSeriesSuggest] = useState(false);
  const [series, setSeries] = useState<TrendSeries | null>(null);
  const [seriesLoading, setSeriesLoading] = useState(false);
  const [seriesError, setSeriesError] = useState<string | null>(null);

  // PR #2: drill-down theo keyword (top papers/authors/journals/co-occurring).
  const [topPapers, setTopPapers] = useState<TrendPremiumPaper[]>([]);
  const [topAuthors, setTopAuthors] = useState<TrendPremiumAuthor[]>([]);
  const [topJournals, setTopJournals] = useState<TrendPremiumJournal[]>([]);
  const [coKeywords, setCoKeywords] = useState<CoOccurringKeyword[]>([]);
  const [drillLoading, setDrillLoading] = useState(false);
  // Tập journalId / authorId đang follow (để nút Follow đổi trạng thái).
  const [followedJournals, setFollowedJournals] = useState<Set<string>>(new Set());
  const [followedAuthors, setFollowedAuthors] = useState<Set<string>>(new Set());

  useEffect(() => {
    getMyFollows()
      .then((fs) => {
        setFollowedJournals(new Set(fs.filter((f) => f.targetType === 'journal').map((f) => f.targetId)));
        setFollowedAuthors(new Set(fs.filter((f) => f.targetType === 'author').map((f) => f.targetId)));
      })
      .catch(() => {});
  }, []);

  const onToggleJournalFollow = async (journalId: string) => {
    try {
      const res = await toggleFollow('journal', journalId);
      setFollowedJournals((prev) => {
        const next = new Set(prev);
        if (res.isFollowing) next.add(journalId);
        else next.delete(journalId);
        return next;
      });
    } catch { /* ignore */ }
  };

  const onToggleAuthorFollow = async (authorId: string) => {
    try {
      const res = await toggleFollow('author', authorId);
      setFollowedAuthors((prev) => {
        const next = new Set(prev);
        if (res.isFollowing) next.add(authorId);
        else next.delete(authorId);
        return next;
      });
    } catch { /* ignore */ }
  };

  useEffect(() => {
    getTrendTop('keyword', { topN: 10, sortBy: 'share' })
      .then((items) => setTopKeywords(items))
      .catch((e) => setError(e instanceof ApiError ? e.message : 'Could not load trend data.'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (!seriesKeyword) return;
    setSeriesLoading(true);
    setSeriesError(null);
    getTrendSeries('keyword', seriesKeyword, { fromYear: 2015, toYear: new Date().getFullYear(), groupBy: 'year' })
      .then(setSeries)
      .catch((e) => {
        setSeries(null);
        setSeriesError(e instanceof ApiError ? e.message : 'Could not load the time series.');
      })
      .finally(() => setSeriesLoading(false));
  }, [seriesKeyword]);

  // Tải drill-down (top papers/authors/journals/co-occurring) cho keyword đang xem.
  useEffect(() => {
    if (!seriesKeyword) {
      setTopPapers([]); setTopAuthors([]); setTopJournals([]); setCoKeywords([]);
      return;
    }
    setDrillLoading(true);
    Promise.allSettled([
      getKeywordTopPapers(seriesKeyword),
      getKeywordTopAuthors(seriesKeyword),
      getKeywordTopJournals(seriesKeyword),
      getCoOccurringKeywords(seriesKeyword),
    ])
      .then(([p, a, j, c]) => {
        setTopPapers(p.status === 'fulfilled' ? p.value : []);
        setTopAuthors(a.status === 'fulfilled' ? a.value : []);
        setTopJournals(j.status === 'fulfilled' ? j.value : []);
        setCoKeywords(c.status === 'fulfilled' ? c.value : []);
      })
      .finally(() => setDrillLoading(false));
  }, [seriesKeyword]);

  // Autocomplete cho o keyword: backend tra theo thu tu nhieu bai -> it.
  useEffect(() => {
    const term = seriesInput.trim();
    if (term.length < 2) {
      setSeriesSuggestions([]);
      return;
    }
    const id = setTimeout(() => {
      suggestKeywords(term, 10)
        .then(setSeriesSuggestions)
        .catch(() => setSeriesSuggestions([]));
    }, 250);
    return () => clearTimeout(id);
  }, [seriesInput]);

  const pickSeries = (s: string) => {
    setSeriesInput(s);
    setSeriesKeyword(s);
    setShowSeriesSuggest(false);
  };

  const maxShare = topKeywords.reduce((m, k) => Math.max(m, k.share), 0) || 1;

  const [exporting, setExporting] = useState(false);
  const exportCsv = async () => {
    if (topKeywords.length === 0) return;
    setExporting(true);
    try {
      // Keyword tiêu điểm để lấy chi tiết: đang xem -> dùng luôn; chưa chọn -> lấy keyword top #1.
      const focus = seriesKeyword || topKeywords[0].name;

      // Nếu chi tiết trong state chưa phải của "focus" thì tải on-demand để báo cáo luôn đầy đủ.
      let s: TrendSeries | null = series;
      let papers: TrendPremiumPaper[] = topPapers;
      let authors: TrendPremiumAuthor[] = topAuthors;
      let journals: TrendPremiumJournal[] = topJournals;
      let co: CoOccurringKeyword[] = coKeywords;
      if (focus !== seriesKeyword) {
        const [sr, pr, ar, jr, cr] = await Promise.allSettled([
          getTrendSeries('keyword', focus, { fromYear: 2015, toYear: new Date().getFullYear(), groupBy: 'year' }),
          getKeywordTopPapers(focus),
          getKeywordTopAuthors(focus),
          getKeywordTopJournals(focus),
          getCoOccurringKeywords(focus),
        ]);
        s = sr.status === 'fulfilled' ? sr.value : null;
        papers = pr.status === 'fulfilled' ? pr.value : [];
        authors = ar.status === 'fulfilled' ? ar.value : [];
        journals = jr.status === 'fulfilled' ? jr.value : [];
        co = cr.status === 'fulfilled' ? cr.value : [];
      }

      // Escape 1 ô CSV (bọc ngoặc kép nếu chứa dấu phẩy / xuống dòng / ngoặc kép).
      const esc = (v: unknown) => {
        const str = String(v ?? '');
        return /[",\n]/.test(str) ? `"${str.replace(/"/g, '""')}"` : str;
      };
      const row = (arr: unknown[]) => arr.map(esc).join(',');
      const lines: string[] = [];
      const now = new Date();

      // ----- Tiêu đề báo cáo -----
      lines.push(row(['Academic Clarity — Trend Analytics Report']));
      lines.push(row(['Generated at', now.toLocaleString('vi-VN')]));
      lines.push(row(['Focus keyword', focus]));
      lines.push('');

      // ----- 1) Xu hướng từ khóa -----
      lines.push(row(['# Top Trending Keywords']));
      lines.push(row(['Keyword', 'Count', 'Range Total', 'Share (%)', 'Slope', 'Direction']));
      topKeywords.forEach((k) => lines.push(row([k.name, k.count, k.rangeTotal, k.share, k.slope, k.direction])));
      lines.push('');

      // ----- 2) Chuỗi thời gian của keyword tiêu điểm -----
      if (s) {
        lines.push(row([`# Publication Volume Trend — "${focus}" (direction: ${s.direction}, slope: ${s.slope})`]));
        lines.push(row(['Period', 'Count', 'Period Total', 'Share (%)']));
        s.series.forEach((p) => lines.push(row([p.period, p.count, p.periodTotal, p.share])));
        lines.push('');
      }

      // ----- 3) Top bài báo -----
      if (papers.length) {
        lines.push(row([`# Top Papers — "${focus}"`]));
        lines.push(row(['Title', 'Year', 'Citations', 'Journal', 'Source URL']));
        papers.forEach((p) => lines.push(row([p.title, p.publicationYear ?? '', p.citationCount, p.journalName, p.sourceUrl])));
        lines.push('');
      }

      // ----- 4) Top tác giả -----
      if (authors.length) {
        lines.push(row([`# Top Authors — "${focus}"`]));
        lines.push(row(['Author', 'Paper Count']));
        authors.forEach((a) => lines.push(row([a.fullName, a.paperCount])));
        lines.push('');
      }

      // ----- 5) Top tạp chí -----
      if (journals.length) {
        lines.push(row([`# Top Journals — "${focus}"`]));
        lines.push(row(['Journal', 'Paper Count']));
        journals.forEach((j) => lines.push(row([j.journalName, j.paperCount])));
        lines.push('');
      }

      // ----- 6) Từ khóa đồng xuất hiện -----
      if (co.length) {
        lines.push(row([`# Co-occurring Keywords — "${focus}"`]));
        lines.push(row(['Keyword', 'Co-occurrence Count']));
        co.forEach((c) => lines.push(row([c.keywordName, c.coOccurrenceCount])));
        lines.push('');
      }

      // BOM (﻿) để Excel đọc UTF-8 đúng (tiếng Việt không lỗi font).
      const blob = new Blob(['﻿' + lines.join('\n')], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `trend-report-${now.toISOString().slice(0, 10)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-gray-400">
            Journal & Keywords · Trend Analytics Dashboard
          </p>
          <h1 className="text-2xl font-bold text-gray-900">Basic Trend Dashboard</h1>
        </div>

        {/* FR-22: nút Export chỉ enable khi có EXPORT_CSV (PREMIUM hoặc ADMIN) */}
        <button
          disabled={!canExportCsv || topKeywords.length === 0 || exporting}
          onClick={exportCsv}
          title={!canExportCsv ? 'Upgrade to Premium to export CSV' : undefined}
          className={`flex items-center gap-2 rounded-md px-4 py-2 text-sm font-medium ${
            canExportCsv && topKeywords.length > 0 && !exporting
              ? 'bg-indigo-700 text-white hover:bg-indigo-800'
              : 'cursor-not-allowed bg-gray-200 text-gray-400'
          }`}
        >
          {exporting ? 'Preparing report…' : 'Export Report (CSV)'}
        </button>
      </div>

      {/* FR-19: Publication Volume Trend - DASHBOARD_BASIC */}
      <RequireFeature feature={FeaturePermission.DASHBOARD_BASIC} featureLabel="Publication Volume Trend">
        <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <div>
              <h2 className="text-lg font-semibold text-gray-800">Publication Volume Trend</h2>
              <p className="text-sm text-gray-500">
                Publications per year for keyword{seriesKeyword ? `: “${seriesKeyword}”` : ''}.
              </p>
            </div>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                if (seriesInput.trim()) {
                  setSeriesKeyword(seriesInput.trim());
                  setShowSeriesSuggest(false);
                }
              }}
              className="flex items-center gap-2"
            >
              <div className="relative">
                <input
                  value={seriesInput}
                  onChange={(e) => {
                    setSeriesInput(e.target.value);
                    setShowSeriesSuggest(true);
                  }}
                  onFocus={() => setShowSeriesSuggest(true)}
                  onBlur={() => setTimeout(() => setShowSeriesSuggest(false), 120)}
                  placeholder="Type a keyword…"
                  className="w-56 rounded-md border border-gray-200 px-3 py-1.5 text-sm"
                />
                {showSeriesSuggest && seriesSuggestions.length > 0 && (
                  <ul className="absolute z-20 mt-1 max-h-72 w-56 overflow-y-auto rounded-md border border-gray-200 bg-white shadow-lg">
                    {seriesSuggestions.map((s) => (
                      <li key={s}>
                        <button
                          type="button"
                          onMouseDown={(e) => e.preventDefault()}
                          onClick={() => pickSeries(s)}
                          className="block w-full px-3 py-2 text-left text-sm text-gray-700 hover:bg-indigo-50"
                        >
                          {s}
                        </button>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
              <button
                type="submit"
                className="rounded-md bg-indigo-700 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-800"
              >
                View
              </button>
            </form>
          </div>

          <div className="mt-4 h-64">
            {!seriesKeyword && !seriesLoading && (
              <div className="flex h-full items-center justify-center text-sm text-gray-400">
                Enter a keyword above to view the chart.
              </div>
            )}
            {seriesLoading && (
              <div className="flex h-full items-center justify-center text-sm text-gray-400">Loading…</div>
            )}
            {seriesError && (
              <div className="flex h-full items-center justify-center text-sm text-red-600">{seriesError}</div>
            )}
            {seriesKeyword && !seriesLoading && !seriesError && series && series.series.length > 0 && (
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={series.series} margin={{ top: 8, right: 16, bottom: 8, left: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#eee" />
                  <XAxis dataKey="period" tick={{ fontSize: 12 }} />
                  <YAxis allowDecimals={false} tick={{ fontSize: 12 }} />
                  <Tooltip
                    formatter={(value) => [`${value} papers`, 'Papers']}
                    labelFormatter={(label) => `Year ${label}`}
                  />
                  <Line type="monotone" dataKey="count" stroke="#4338ca" strokeWidth={2} dot={{ r: 3 }} />
                </LineChart>
              </ResponsiveContainer>
            )}
            {seriesKeyword && !seriesLoading && !seriesError && (!series || series.series.length === 0) && (
              <div className="flex h-full items-center justify-center text-sm text-gray-400">
                No data for this keyword.
              </div>
            )}
          </div>

          {series && (
            <p className="mt-2 text-xs text-gray-500">
              {series.totalPapers} papers total · trend:{' '}
              <span
                className={
                  series.direction === 'rising'
                    ? 'text-green-600'
                    : series.direction === 'falling'
                      ? 'text-red-600'
                      : 'text-gray-500'
                }
              >
                {series.direction === 'rising' ? 'rising ▲' : series.direction === 'falling' ? 'falling ▼' : 'stable'}
              </span>
            </p>
          )}

          {/* PR #2: drill-down theo keyword đang xem (top papers/authors/journals/co-occurring). */}
          {seriesKeyword && (
            <div className="mt-6 border-t border-gray-100 pt-4">
              <h3 className="text-sm font-semibold text-gray-800">
                Insights for “{seriesKeyword}”
              </h3>
              {drillLoading && <p className="mt-2 text-sm text-gray-400">Loading insights…</p>}
              {!drillLoading && (
                <div className="mt-3 grid gap-4 md:grid-cols-2">
                  {/* Top papers */}
                  <div className="flex flex-col rounded-lg border border-gray-100">
                    <div className="flex items-center justify-between border-b border-gray-100 px-3 py-2">
                      <p className="text-xs font-semibold uppercase tracking-wide text-gray-500">Top papers</p>
                      <span className="rounded-full bg-gray-100 px-2 py-0.5 text-[11px] text-gray-500">{topPapers.length}</span>
                    </div>
                    <div className="max-h-72 overflow-y-auto px-3 py-2">
                      {topPapers.length === 0 ? (
                        <p className="text-xs text-gray-400">—</p>
                      ) : (
                        <ol className="space-y-2">
                          {topPapers.map((p, i) => (
                            <li key={p.paperId} className="flex gap-2 text-sm">
                              <span className="w-4 shrink-0 text-right text-xs text-gray-300">{i + 1}</span>
                              <span className="min-w-0">
                                <Link
                                  to={`/papers/${encodeURIComponent(p.paperId)}`}
                                  className="line-clamp-2 text-indigo-700 hover:underline"
                                >
                                  {p.title}
                                </Link>
                                <span className="text-[11px] text-gray-400">
                                  {p.publicationYear ?? '—'} · {p.citationCount.toLocaleString()} citations
                                </span>
                              </span>
                            </li>
                          ))}
                        </ol>
                      )}
                    </div>
                  </div>

                  {/* Top authors */}
                  <div className="flex flex-col rounded-lg border border-gray-100">
                    <div className="flex items-center justify-between border-b border-gray-100 px-3 py-2">
                      <p className="text-xs font-semibold uppercase tracking-wide text-gray-500">Top authors</p>
                      <span className="rounded-full bg-gray-100 px-2 py-0.5 text-[11px] text-gray-500">{topAuthors.length}</span>
                    </div>
                    <div className="max-h-72 overflow-y-auto px-3 py-2">
                      {topAuthors.length === 0 ? (
                        <p className="text-xs text-gray-400">—</p>
                      ) : (
                        <ul className="divide-y divide-gray-50">
                          {topAuthors.map((a) => (
                            <li key={a.authorId} className="flex items-center justify-between gap-2 py-1.5 text-sm">
                              <span className="truncate text-gray-700">{a.fullName}</span>
                              <span className="flex shrink-0 items-center gap-2">
                                <span className="rounded-full bg-indigo-50 px-2 py-0.5 text-[11px] text-indigo-700">{a.paperCount}</span>
                                <button
                                  type="button"
                                  onClick={() => onToggleAuthorFollow(String(a.authorId))}
                                  className={`rounded-md border px-2 py-0.5 text-[11px] ${
                                    followedAuthors.has(String(a.authorId))
                                      ? 'border-indigo-600 bg-indigo-600 text-white'
                                      : 'border-gray-300 text-gray-600 hover:bg-gray-50'
                                  }`}
                                >
                                  {followedAuthors.has(String(a.authorId)) ? 'Following' : 'Follow'}
                                </button>
                              </span>
                            </li>
                          ))}
                        </ul>
                      )}
                    </div>
                  </div>

                  {/* Top journals */}
                  <div className="flex flex-col rounded-lg border border-gray-100">
                    <div className="flex items-center justify-between border-b border-gray-100 px-3 py-2">
                      <p className="text-xs font-semibold uppercase tracking-wide text-gray-500">Top journals</p>
                      <span className="rounded-full bg-gray-100 px-2 py-0.5 text-[11px] text-gray-500">{topJournals.length}</span>
                    </div>
                    <div className="max-h-72 overflow-y-auto px-3 py-2">
                      {topJournals.length === 0 ? (
                        <p className="text-xs text-gray-400">—</p>
                      ) : (
                        <ul className="divide-y divide-gray-50">
                          {topJournals.map((j) => (
                            <li key={j.journalId} className="flex items-center justify-between gap-2 py-1.5 text-sm">
                              <span className="truncate text-gray-700">{j.journalName}</span>
                              <span className="flex shrink-0 items-center gap-2">
                                <span className="rounded-full bg-indigo-50 px-2 py-0.5 text-[11px] text-indigo-700">{j.paperCount}</span>
                                <button
                                  type="button"
                                  onClick={() => onToggleJournalFollow(j.journalId)}
                                  className={`rounded-md border px-2 py-0.5 text-[11px] ${
                                    followedJournals.has(j.journalId)
                                      ? 'border-indigo-600 bg-indigo-600 text-white'
                                      : 'border-gray-300 text-gray-600 hover:bg-gray-50'
                                  }`}
                                >
                                  {followedJournals.has(j.journalId) ? 'Following' : 'Follow'}
                                </button>
                              </span>
                            </li>
                          ))}
                        </ul>
                      )}
                    </div>
                  </div>

                  {/* Co-occurring keywords */}
                  <div className="flex flex-col rounded-lg border border-gray-100">
                    <div className="flex items-center justify-between border-b border-gray-100 px-3 py-2">
                      <p className="text-xs font-semibold uppercase tracking-wide text-gray-500">Co-occurring keywords</p>
                      <span className="rounded-full bg-gray-100 px-2 py-0.5 text-[11px] text-gray-500">{coKeywords.length}</span>
                    </div>
                    <div className="max-h-72 overflow-y-auto px-3 py-2">
                      {coKeywords.length === 0 ? (
                        <p className="text-xs text-gray-400">—</p>
                      ) : (
                        <div className="flex flex-wrap gap-2">
                          {coKeywords.map((k) => (
                            <span key={k.keywordName} className="rounded-full bg-indigo-50 px-3 py-1 text-xs text-indigo-700">
                              {k.keywordName} <span className="text-indigo-400">· {k.coOccurrenceCount}</span>
                            </span>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              )}
              <p className="mt-3 text-xs text-gray-400">
                Basic accounts see 2 items per category; upgrade to Premium to see the full list.
              </p>
            </div>
          )}
        </div>
      </RequireFeature>

      {/* FR-20/21: Top Trending Keywords + date filter nâng cao - DASHBOARD_ADVANCED */}
      <RequireFeature
        feature={FeaturePermission.DASHBOARD_ADVANCED}
        featureLabel="Top Trending Keywords"
        description="Unlock the stacked bar chart of top keywords and the custom year filter when you upgrade to Premium."
      >
        <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="text-lg font-semibold text-gray-800">Top Trending Keywords</h2>
          <p className="text-sm text-gray-500">Highest volume academic concepts by frequency.</p>

          {loading && <p className="mt-4 text-sm text-gray-400">Loading…</p>}
          {error && <p className="mt-4 text-sm text-red-600">{error}</p>}
          {!loading && !error && topKeywords.length === 0 && (
            <p className="mt-4 text-sm text-gray-400">No trend data yet.</p>
          )}

          {!loading && !error && topKeywords.length > 0 && (
            <div className="mt-4 space-y-2 text-sm text-gray-700">
              {topKeywords.map((kw) => (
                <div key={kw.name}>
                  <div className="flex items-center justify-between">
                    <span className="font-medium">{kw.name}</span>
                    <span className="text-xs text-gray-500">
                      {kw.count} papers · {(kw.share * 100).toFixed(1)}%
                      {kw.direction === 'rising' ? ' ▲' : kw.direction === 'falling' ? ' ▼' : ''}
                    </span>
                  </div>
                  <div className="mt-1 h-2 rounded bg-gray-100">
                    <div
                      className="h-2 rounded bg-indigo-700"
                      style={{ width: `${Math.max(4, (kw.share / maxShare) * 100)}%` }}
                    />
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </RequireFeature>
    </div>
  );
};

export default TrendDashboardPage;

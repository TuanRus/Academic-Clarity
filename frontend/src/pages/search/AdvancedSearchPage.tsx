import { useState } from 'react';
import { Link } from 'react-router-dom';
import { searchPapers } from '../../lib/api/mindmap';
import { ApiError } from '../../lib/http';
import type { PaperSearchItem } from '../../types/api';

// R-01 · Research Search Screen (Advanced) - FR-06, FR-07, FR-08
// ĐÃ NỐI API thật: GET /api/mindmap/search (q/page/pageSize + fromYear/toYear).
// Bộ lọc Year Range nối backend; Research Field chỉ tham khảo (corpus hiện toàn Computer Science).
const AdvancedSearchPage = () => {
  const [tab, setTab] = useState<'keyword' | 'doi'>('keyword');
  const [query, setQuery] = useState('');
  const [fromYear, setFromYear] = useState(2015);
  const [toYear, setToYear] = useState(2025);
  const [results, setResults] = useState<PaperSearchItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);

  const runSearch = async () => {
    const q = query.trim();
    if (!q) return;
    setLoading(true);
    setError(null);
    setSubmitted(true);
    try {
      const paged = await searchPapers(q, 1, 20, { fromYear, toYear });
      setResults(paged.items);
      setTotal(paged.totalCount);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Could not connect to the server.');
      setResults([]);
      setTotal(0);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="grid gap-6 lg:grid-cols-[280px_1fr]">
      <aside className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <h2 className="text-sm font-semibold text-gray-800">Filter Results</h2>
        <p className="mt-1 text-xs text-gray-400">
          Year range áp dụng khi bấm Search. Research Field chỉ tham khảo (corpus hiện toàn Computer Science).
        </p>

        <div className="mt-4">
          <p className="text-xs font-medium text-gray-500">YEAR RANGE</p>
          <div className="mt-2 flex items-center gap-2">
            <input
              type="number"
              min={1900}
              max={toYear}
              value={fromYear}
              onChange={(e) => setFromYear(Number(e.target.value))}
              className="w-20 rounded-md border border-gray-200 px-2 py-1 text-sm"
            />
            <span className="text-xs text-gray-400">–</span>
            <input
              type="number"
              min={fromYear}
              max={2100}
              value={toYear}
              onChange={(e) => setToYear(Number(e.target.value))}
              className="w-20 rounded-md border border-gray-200 px-2 py-1 text-sm"
            />
          </div>
        </div>

        <div className="mt-4">
          <p className="text-xs font-medium text-gray-500">RESEARCH FIELD</p>
          {['Computer Science', 'Artificial Intelligence', 'Mathematics', 'Physics'].map((f) => (
            <label key={f} className="mt-2 flex items-center gap-2 text-sm text-gray-400">
              <input type="checkbox" disabled />
              {f}
            </label>
          ))}
        </div>
      </aside>

      <section className="space-y-4">
        <h1 className="text-2xl font-bold text-gray-900">Advanced Research Discovery</h1>

        <div className="flex gap-2 border-b border-gray-200">
          <button
            onClick={() => setTab('keyword')}
            className={`px-3 py-2 text-sm ${
              tab === 'keyword' ? 'border-b-2 border-indigo-700 font-semibold text-indigo-700' : 'text-gray-500'
            }`}
          >
            Keyword Search
          </button>
          <button
            onClick={() => setTab('doi')}
            className={`px-3 py-2 text-sm ${
              tab === 'doi' ? 'border-b-2 border-indigo-700 font-semibold text-indigo-700' : 'text-gray-500'
            }`}
          >
            DOI & OpenAlex ID Search
          </button>
        </div>

        <form
          onSubmit={(e) => {
            e.preventDefault();
            runSearch();
          }}
          className="flex gap-2"
        >
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={tab === 'keyword' ? 'Machine Learning' : '10.5555/3295222.3295349 or W2741809807'}
            className="w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
          />
          <button
            type="submit"
            className="shrink-0 rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800"
          >
            Search
          </button>
        </form>

        {(loading || error || submitted) && (
          <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
            {loading && <p className="text-sm text-gray-400">Searching…</p>}
            {error && <p className="text-sm text-red-600">{error}</p>}
            {!loading && !error && (
              <>
                <h2 className="mb-3 text-sm font-semibold text-gray-800">{total} results</h2>
                {results.length === 0 ? (
                  <p className="text-sm text-gray-400">No papers found.</p>
                ) : (
                  <ul className="divide-y divide-gray-100">
                    {results.map((p) => (
                      <li key={p.paperId} className="py-3">
                        <Link
                          to={`/papers/${encodeURIComponent(p.paperId)}`}
                          className="text-sm font-medium text-indigo-700 hover:underline"
                        >
                          {p.title}
                        </Link>
                        <p className="mt-1 text-xs text-gray-500">
                          {p.journalName || 'Unknown journal'}
                          {p.year ? ` · ${p.year}` : ''} · {p.citationCount} citations
                          {p.quartile ? ` · ${p.quartile}` : ''}
                        </p>
                      </li>
                    ))}
                  </ul>
                )}
              </>
            )}
          </div>
        )}
      </section>
    </div>
  );
};

export default AdvancedSearchPage;

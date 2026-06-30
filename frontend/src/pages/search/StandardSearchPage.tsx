import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { searchPapers, searchPapersByAuthor, searchPapersByJournal, suggestKeywords } from '../../lib/api/mindmap';
import { getHistory, saveHistory, clearHistory } from '../../lib/api/searchHistory';
import { ApiError } from '../../lib/http';
import type { PaperSearchItem } from '../../types/api';

type SearchScope = 'all' | 'author' | 'journal';

// LS-01 · Search Screen (Standard) - FR-06, FR-07, FR-08, FR-09, FR-10
// ĐÃ NỐI API thật: autocomplete (GET /api/mindmap/keywords/suggest) + kết quả (GET /api/mindmap/search).
const StandardSearchPage = () => {
  const [query, setQuery] = useState('');
  const [scope, setScope] = useState<SearchScope>('all');
  const [suggestions, setSuggestions] = useState<string[]>([]);
  const [showSuggestions, setShowSuggestions] = useState(false);
  // Lịch sử tìm kiếm: ĐÃ NỐI BACKEND (GET/POST/DELETE /api/search/history).
  const [recent, setRecent] = useState<string[]>([]);

  const reloadHistory = () => {
    getHistory(8)
      .then((items) => {
        const seen = new Set<string>();
        const texts: string[] = [];
        for (const it of items) {
          if (!seen.has(it.searchText)) {
            seen.add(it.searchText);
            texts.push(it.searchText);
          }
        }
        setRecent(texts);
      })
      .catch(() => setRecent([]));
  };

  useEffect(() => {
    reloadHistory();
  }, []);

  const [results, setResults] = useState<PaperSearchItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);

  // FR-08: autocomplete keyword (chỉ khi tìm theo "All" — author/journal không có gợi ý).
  useEffect(() => {
    const term = query.trim();
    if (scope !== 'all' || term.length < 2) {
      setSuggestions([]);
      return;
    }
    const id = setTimeout(() => {
      suggestKeywords(term, 8)
        .then(setSuggestions)
        .catch(() => setSuggestions([]));
    }, 250);
    return () => clearTimeout(id);
  }, [query, scope]);

  const runSearch = async (term: string) => {
    const q = term.trim();
    if (!q) return;
    setLoading(true);
    setError(null);
    setSubmitted(true);
    setShowSuggestions(false);
    try {
      const paged =
        scope === 'author'
          ? await searchPapersByAuthor(q, 1, 20)
          : scope === 'journal'
            ? await searchPapersByJournal(q, 1, 20)
            : await searchPapers(q, 1, 20);
      setResults(paged.items);
      setTotal(paged.totalCount);

      // Optimistic + lưu xuống BE rồi reload từ server.
      setRecent((prev) => [q, ...prev.filter((r) => r !== q)].slice(0, 8));
      saveHistory(q, scope === 'all' ? 'keyword' : scope)
        .then(() => reloadHistory())
        .catch(() => {});
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Could not connect to the server.');
      setResults([]);
      setTotal(0);
    } finally {
      setLoading(false);
    }
  };

  // Chọn 1 gợi ý: điền + tìm luôn, và ẩn dropdown (tránh hiện lại do effect chạy lại).
  const pick = (s: string) => {
    setQuery(s);
    setShowSuggestions(false);
    runSearch(s);
  };

  const inputRef = useRef<HTMLInputElement>(null);

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Standard Search</h1>
        <p className="text-sm text-gray-500">
          Search by keyword or by exact DOI / OpenAlex ID.
        </p>
      </div>

      <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            runSearch(query);
          }}
          className="flex gap-2"
        >
          {/* List box chọn phạm vi tìm: All (keyword/DOI) / Author / Journal */}
          <select
            value={scope}
            onChange={(e) => {
              setScope(e.target.value as SearchScope);
              setShowSuggestions(false);
            }}
            className="shrink-0 rounded-md border border-gray-200 px-2 py-2 text-sm"
          >
            <option value="all">Keyword / DOI / OpenAlex ID</option>
            <option value="author">Author</option>
            <option value="journal">Journal</option>
          </select>
          <input
            ref={inputRef}
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setShowSuggestions(true);
            }}
            onFocus={() => setShowSuggestions(true)}
            onBlur={() => setTimeout(() => setShowSuggestions(false), 120)}
            placeholder={
              scope === 'author'
                ? 'Search by author name…'
                : scope === 'journal'
                  ? 'Search by journal name…'
                  : 'Search papers by keyword, DOI, or OpenAlex ID...'
            }
            className="w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
          />
          <button
            type="submit"
            className="shrink-0 rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800"
          >
            Search
          </button>
        </form>

        {/* FR-08: autocomplete dropdown (du lieu that, sap xep nhieu bai -> it) */}
        {scope === 'all' && showSuggestions && suggestions.length > 0 && (
          <ul className="mt-2 divide-y divide-gray-100 rounded-md border border-gray-200">
            {suggestions.map((s) => (
              <li key={s}>
                <button
                  type="button"
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => pick(s)}
                  className="block w-full px-3 py-2 text-left text-sm text-gray-700 hover:bg-indigo-50"
                >
                  {s}
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* FR-09 / FR-10: lịch sử tìm kiếm gần đây (localStorage) */}
      <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-semibold text-gray-800">Recent Searches</h2>
          <button
            onClick={() => {
              setRecent([]);
              clearHistory().catch(() => {});
            }}
            className="text-xs text-red-600 hover:underline"
          >
            Clear history
          </button>
        </div>
        <div className="mt-2 flex flex-wrap gap-2">
          {recent.map((term) => (
            <button
              key={term}
              onClick={() => {
                setQuery(term);
                runSearch(term);
              }}
              className="rounded-full bg-indigo-50 px-3 py-1 text-xs text-indigo-700"
            >
              {term}
            </button>
          ))}
          {recent.length === 0 && <p className="text-xs text-gray-400">No recent searches.</p>}
        </div>
      </div>

      {/* Kết quả tìm kiếm */}
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
    </div>
  );
};

export default StandardSearchPage;

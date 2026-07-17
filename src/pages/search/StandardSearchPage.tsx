import { useState } from 'react';
import { RECENT_SEARCHES, AUTOCOMPLETE_SUGGESTIONS } from '../../mock/searchSuggestions';

// LS-01 · Search Screen (Standard) - FR-06, FR-07, FR-08, FR-09, FR-10
const StandardSearchPage = () => {
  const [query, setQuery] = useState('');
  const [recent, setRecent] = useState(RECENT_SEARCHES);

  const suggestions = AUTOCOMPLETE_SUGGESTIONS[query.trim().toLowerCase()] ?? [];

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Standard Search</h1>
        <p className="text-sm text-gray-500">
          Search by keyword (FR-06) or by exact DOI / OpenAlex ID (FR-07).
        </p>
      </div>

      <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <input
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search papers by keyword, DOI, or OpenAlex ID..."
          className="w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
        />

        {/* FR-08: autocomplete dropdown */}
        {suggestions.length > 0 && (
          <ul className="mt-2 divide-y divide-gray-100 rounded-md border border-gray-200">
            {suggestions.map((s) => (
              <li key={s} className="px-3 py-2 text-sm text-gray-700 hover:bg-indigo-50">
                {s}
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* FR-09 / FR-10: lịch sử tìm kiếm gần đây */}
      <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-semibold text-gray-800">Recent Searches</h2>
          <button onClick={() => setRecent([])} className="text-xs text-red-600 hover:underline">
            Clear history
          </button>
        </div>
        <div className="mt-2 flex flex-wrap gap-2">
          {recent.map((term) => (
            <button
              key={term}
              onClick={() => setQuery(term)}
              className="rounded-full bg-indigo-50 px-3 py-1 text-xs text-indigo-700"
            >
              {term}
            </button>
          ))}
          {recent.length === 0 && <p className="text-xs text-gray-400">No recent searches.</p>}
        </div>
      </div>
    </div>
  );
};

export default StandardSearchPage;

import { useState, useRef, useEffect } from 'react';

// LS-01 · Search Screen (Standard) — FR-06, FR-07, FR-08, FR-09, FR-10

const FIELDS_OF_STUDY = ['Computer Science', 'Artificial Intelligence', 'Data Science', 'Mathematics'];

const ALL_SUGGESTIONS = [
  'Machine Learning',
  'Machine Learning Introduction for Undergraduates',
  'Machine Learning for Biomedical Research',
  'Machine Learning in Structural Engineering',
  'Deep Learning',
  'Natural Language Processing',
  'Computer Vision',
  'Transformer Architecture',
  'Graph Neural Networks',
  'Reinforcement Learning',
  'Data Mining Techniques',
  'Neural Network Optimization',
  'Large Language Models',
  'Attention Mechanism in NLP',
  'Transfer Learning',
];

const INITIAL_RECENT_SEARCHES = ['Natural Language Processing', 'Computer Vision', 'ESP32 Automation'];

type SuggestionItem = { text: string; isHistory: boolean };

function buildSuggestions(query: string, recentSearches: string[]): SuggestionItem[] {
  const trimmed = query.trim();
  if (!trimmed) return [];
  const lower = trimmed.toLowerCase();

  const historyMatch = recentSearches.find((r) => r.toLowerCase().includes(lower));
  const items: SuggestionItem[] = [];
  if (historyMatch) items.push({ text: historyMatch, isHistory: true });

  for (const s of ALL_SUGGESTIONS) {
    if (items.length >= 4) break;
    if (items.some((i) => i.text === s)) continue;
    if (s.toLowerCase().includes(lower)) items.push({ text: s, isHistory: false });
  }

  return items.slice(0, 4); // 1 history + up to 3 suggestions
}

function HighlightMatch({ text, query }: { text: string; query: string }) {
  const lower = text.toLowerCase();
  const idx = lower.indexOf(query.toLowerCase());
  if (idx === -1) return <span>{text}</span>;
  return (
    <span>
      {text.slice(0, idx)}
      <span className="font-bold">{text.slice(idx, idx + query.length)}</span>
      {text.slice(idx + query.length)}
    </span>
  );
}

const SearchIcon = ({ className = '' }: { className?: string }) => (
  <svg className={className} xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
    <path strokeLinecap="round" strokeLinejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
  </svg>
);

const ClockIcon = ({ className = '' }: { className?: string }) => (
  <svg className={className} xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
    <path strokeLinecap="round" strokeLinejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
  </svg>
);

const SparkleIcon = ({ className = '' }: { className?: string }) => (
  <svg className={className} xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
    <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904 9 18.75l-.813-2.846a4.5 4.5 0 0 0-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 0 0 3.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 0 0 3.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 0 0-3.09 3.09ZM18.259 8.715 18 9.75l-.259-1.035a3.375 3.375 0 0 0-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 0 0 2.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 0 0 2.456 2.456L21.75 6l-1.035.259a3.375 3.375 0 0 0-2.456 2.456ZM16.894 20.567 16.5 21.75l-.394-1.183a2.25 2.25 0 0 0-1.423-1.423L13.5 18.75l1.183-.394a2.25 2.25 0 0 0 1.423-1.423l.394-1.183.394 1.183a2.25 2.25 0 0 0 1.423 1.423l1.183.394-1.183.394a2.25 2.25 0 0 0-1.423 1.423Z" />
  </svg>
);

const XMarkIcon = ({ className = '' }: { className?: string }) => (
  <svg className={className} xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
    <path strokeLinecap="round" strokeLinejoin="round" d="M6 18 18 6M6 6l12 12" />
  </svg>
);

type Tab = 'keyword' | 'doi';

const SearchPage = () => {
  const [query, setQuery] = useState('');
  const [activeTab, setActiveTab] = useState<Tab>('keyword');
  const [yearFrom, setYearFrom] = useState(2015);
  const [checkedFields, setCheckedFields] = useState<Set<string>>(
    new Set(['Computer Science', 'Artificial Intelligence']),
  );
  const [showAutocomplete, setShowAutocomplete] = useState(false);
  const [recentSearches, setRecentSearches] = useState(INITIAL_RECENT_SEARCHES);
  const searchRef = useRef<HTMLDivElement>(null);

  const suggestions = buildSuggestions(query, recentSearches);

  useEffect(() => {
    const handleClick = (e: MouseEvent) => {
      if (searchRef.current && !searchRef.current.contains(e.target as Node)) {
        setShowAutocomplete(false);
      }
    };
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, []);

  const toggleField = (field: string) => {
    setCheckedFields((prev) => {
      const next = new Set(prev);
      if (next.has(field)) next.delete(field);
      else next.add(field);
      return next;
    });
  };

  const handleSelectSuggestion = (text: string) => {
    setQuery(text);
    setShowAutocomplete(false);
  };

  const removeRecent = (term: string) => {
    setRecentSearches((prev) => prev.filter((r) => r !== term));
  };

  return (
    <div className="space-y-3">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-1 text-xs text-gray-500">
        <span>Academic Workspace</span>
        <span className="text-gray-400">›</span>
        <span className="font-semibold text-indigo-800">Standard Search</span>
      </nav>
      <p className="text-xs text-gray-400">LS-01 · Search Screen (Standard)</p>

      <div className="grid grid-cols-12 gap-6 items-start pt-1">
        {/* Left sidebar — filters */}
        <aside className="col-span-3 rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
          <div className="flex items-center justify-between mb-5">
            <h2 className="text-sm font-semibold text-gray-800">Filter Results</h2>
            <button
              onClick={() => {
                setYearFrom(2015);
                setCheckedFields(new Set());
              }}
              className="text-xs text-indigo-700 hover:underline"
            >
              Reset
            </button>
          </div>

          {/* Publication Year slider */}
          <div className="mb-5">
            <label className="block text-xs font-medium text-gray-500 mb-3">Publication Year</label>
            <input
              type="range"
              min={2015}
              max={2025}
              value={yearFrom}
              onChange={(e) => setYearFrom(Number(e.target.value))}
              className="w-full accent-indigo-800"
            />
            <div className="flex justify-between text-xs text-gray-500 mt-1">
              <span>{yearFrom}</span>
              <span>2025</span>
            </div>
          </div>

          <hr className="border-gray-100 mb-5" />

          {/* Field of Study */}
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-3">Field of Study</label>
            <div className="flex flex-col gap-3">
              {FIELDS_OF_STUDY.map((field) => (
                <label key={field} className="flex items-center gap-3 cursor-pointer group">
                  <input
                    type="checkbox"
                    checked={checkedFields.has(field)}
                    onChange={() => toggleField(field)}
                    className="w-4 h-4 rounded border-gray-300 text-indigo-700 focus:ring-indigo-700"
                  />
                  <span className="text-sm text-gray-700 group-hover:text-indigo-800 transition-colors">
                    {field}
                  </span>
                </label>
              ))}
            </div>
            <button className="mt-3 text-xs text-indigo-700 flex items-center gap-1 hover:underline">
              <span>+</span> Show all fields
            </button>
          </div>
        </aside>

        {/* Main content */}
        <section className="col-span-9 flex flex-col gap-5">
          {/* Tabs */}
          <div className="flex bg-gray-100 p-1 rounded-xl self-start gap-1">
            {(['keyword', 'doi'] as Tab[]).map((tab) => (
              <button
                key={tab}
                onClick={() => setActiveTab(tab)}
                className={[
                  'px-5 py-2 rounded-lg text-sm font-medium transition-all',
                  activeTab === tab
                    ? 'bg-white text-indigo-800 shadow-sm'
                    : 'text-gray-500 hover:text-gray-800',
                ].join(' ')}
              >
                {tab === 'keyword' ? 'Keyword Search' : 'DOI & OpenAlex ID Search'}
              </button>
            ))}
          </div>

          {/* Search bar + autocomplete */}
          <div className="relative w-full" ref={searchRef}>
            <div className="flex items-center bg-white h-14 px-4 rounded-xl border border-gray-200 shadow-sm focus-within:border-indigo-700 focus-within:ring-2 focus-within:ring-indigo-100 transition-all">
              <SearchIcon className="w-5 h-5 text-gray-400 mr-3 flex-shrink-0" />
              <input
                type="text"
                value={query}
                onChange={(e) => {
                  setQuery(e.target.value);
                  setShowAutocomplete(true);
                }}
                onFocus={() => setShowAutocomplete(true)}
                placeholder={
                  activeTab === 'keyword'
                    ? 'Search for journals, papers, or keywords...'
                    : 'Enter a DOI (10.xxxx/...) or OpenAlex ID (W1234567890)...'
                }
                className="flex-grow bg-transparent border-none focus:ring-0 text-sm text-gray-900 placeholder-gray-400 outline-none"
              />
              {query && (
                <button
                  onClick={() => {
                    setQuery('');
                    setShowAutocomplete(false);
                  }}
                >
                  <XMarkIcon className="w-5 h-5 text-gray-400 hover:text-gray-600 transition-colors" />
                </button>
              )}
            </div>

            {/* Autocomplete dropdown */}
            {showAutocomplete && suggestions.length > 0 && (
              <div className="absolute top-full left-0 right-0 mt-1.5 bg-white rounded-xl border border-gray-200 shadow-md z-40 overflow-hidden">
                <ul>
                  {suggestions.map((s, i) => (
                    <li
                      key={i}
                      onMouseDown={() => handleSelectSuggestion(s.text)}
                      className="px-4 py-3 hover:bg-indigo-50 cursor-pointer flex items-center gap-3 transition-colors"
                    >
                      {s.isHistory ? (
                        <ClockIcon className="w-5 h-5 text-gray-400 flex-shrink-0" />
                      ) : (
                        <SearchIcon className="w-5 h-5 text-gray-400 flex-shrink-0" />
                      )}
                      <span className="text-sm text-gray-800">
                        <HighlightMatch text={s.text} query={query} />
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>

          {/* Recent search chips */}
          {recentSearches.length > 0 && (
            <div className="flex flex-col gap-3">
              <div className="flex items-center justify-between">
                <h3 className="text-xs font-semibold text-gray-500 uppercase tracking-wider">
                  Recent Searches
                </h3>
                <button
                  onClick={() => setRecentSearches([])}
                  className="text-xs text-indigo-700 hover:underline"
                >
                  Clear All History
                </button>
              </div>
              <div className="flex flex-wrap gap-2">
                {recentSearches.map((term) => (
                  <div
                    key={term}
                    className="flex items-center gap-1.5 bg-gray-50 px-3 py-1.5 rounded-full border border-gray-200 hover:border-indigo-300 transition-colors group"
                  >
                    <span className="text-xs text-gray-600">{term}</span>
                    <button
                      onClick={() => removeRecent(term)}
                      className="text-gray-400 hover:text-red-500 transition-colors"
                    >
                      <XMarkIcon className="w-3 h-3" />
                    </button>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Empty state */}
          <div className="mt-6 border border-dashed border-gray-200 rounded-xl p-16 flex flex-col items-center text-center">
            <div className="w-16 h-16 bg-indigo-50 rounded-full flex items-center justify-center mb-4">
              <SparkleIcon className="w-8 h-8 text-indigo-800" />
            </div>
            <h2 className="text-xl font-semibold text-gray-900 mb-2">Start your exploration</h2>
            <p className="text-sm text-gray-500 max-w-sm">
              Enter keywords, paper titles, or researcher names to begin analyzing the global
              academic landscape with precision.
            </p>
          </div>
        </section>
      </div>
    </div>
  );
};

export default SearchPage;

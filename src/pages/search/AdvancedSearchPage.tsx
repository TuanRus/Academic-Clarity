import { useState } from 'react';

// R-01 · Research Search Screen (Advanced) - FR-06, FR-07, FR-08
const AdvancedSearchPage = () => {
  const [tab, setTab] = useState<'keyword' | 'doi'>('keyword');

  return (
    <div className="grid gap-6 lg:grid-cols-[280px_1fr]">
      <aside className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <h2 className="text-sm font-semibold text-gray-800">Filter Results</h2>

        <div className="mt-4">
          <p className="text-xs font-medium text-gray-500">YEAR RANGE</p>
          <input type="range" min={2015} max={2025} className="mt-2 w-full" />
          <div className="flex justify-between text-xs text-gray-500">
            <span>2015</span>
            <span>2025</span>
          </div>
        </div>

        <div className="mt-4">
          <p className="text-xs font-medium text-gray-500">RESEARCH FIELD</p>
          {['Computer Science', 'Artificial Intelligence', 'Mathematics', 'Physics'].map((f) => (
            <label key={f} className="mt-2 flex items-center gap-2 text-sm text-gray-700">
              <input type="checkbox" defaultChecked={f !== 'Mathematics' && f !== 'Physics'} />
              {f}
            </label>
          ))}
        </div>
      </aside>

      <section className="space-y-4">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Advanced Research Discovery</h1>
        </div>

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

        {tab === 'keyword' ? (
          <input
            placeholder="Machine Learning"
            className="w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
          />
        ) : (
          <input
            placeholder="10.5555/3295222.3295349 or W2741809807"
            className="w-full rounded-md border border-gray-200 px-3 py-2 text-sm"
          />
        )}
      </section>
    </div>
  );
};

export default AdvancedSearchPage;

import { Link } from 'react-router-dom';
import { useBookmark } from '../../hooks/useBookmark';

// LS-05 · Saved Papers Library (Bookmark Manager) - FR-24
const SavedLibraryPage = () => {
  const { bookmarkedPapers, toggleBookmark } = useBookmark();

  return (
    <div className="space-y-4">
      <div>
        <p className="text-xs font-medium uppercase tracking-wide text-gray-400">
          Bookmark Manager
        </p>
        <h1 className="text-2xl font-bold text-gray-900">
          Saved Papers Library ({bookmarkedPapers.length} papers saved)
        </h1>
      </div>

      {bookmarkedPapers.length === 0 && (
        <div className="rounded-xl border border-dashed border-gray-300 bg-white p-10 text-center text-sm text-gray-500">
          No papers bookmarked yet. Open a paper and click "Bookmark Paper".
        </div>
      )}

      {bookmarkedPapers.map((paper) => (
        <div key={paper.paperId} className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <div className="flex items-start justify-between gap-4">
            <div>
              <Link
                to={`/papers/${encodeURIComponent(paper.paperId)}`}
                className="text-base font-semibold text-gray-900 hover:text-indigo-700"
              >
                {paper.title}
              </Link>
              <p className="mt-1 text-sm text-gray-500">
                {paper.year ?? '—'}
                {paper.citationCount != null ? ` · ${paper.citationCount} citations` : ''}
              </p>
              <div className="mt-3 flex flex-wrap gap-2">
                {paper.keywords.map((kw) => (
                  <span key={kw} className="rounded-full bg-indigo-50 px-3 py-1 text-xs text-indigo-700">
                    {kw}
                  </span>
                ))}
              </div>
            </div>
            {/* BR-32: xóa Bookmark cũng xóa Followed Item (NEW_CITATION) tương ứng */}
            <button
              onClick={() => toggleBookmark(paper)}
              className="shrink-0 rounded-md border border-red-600 px-3 py-1.5 text-sm text-red-600 hover:bg-red-50"
            >
              Remove Bookmark
            </button>
          </div>
        </div>
      ))}
    </div>
  );
};

export default SavedLibraryPage;

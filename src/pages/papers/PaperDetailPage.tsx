import { useBookmark } from '../../hooks/useBookmark';
import { SAMPLE_PAPER } from '../../mock/papers';

// LS-03 · Paper Detail Screen - FR-23 (+ BR-32: Bookmark = Follow cho ResearchPaper)
const PaperDetailPage = () => {
  const { isBookmarked, toggleBookmark } = useBookmark();
  const paper = SAMPLE_PAPER;
  const bookmarked = isBookmarked(paper.paperId);

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
      <p className="text-xs font-medium uppercase tracking-wide text-gray-400">
        LS-03 · Paper Detail Screen
      </p>
      <h1 className="mt-1 text-2xl font-bold text-gray-900">{paper.title}</h1>
      <p className="mt-1 text-sm text-gray-700">{paper.authors.join(', ')}</p>
      <p className="text-sm italic text-gray-500">
        {paper.journal} · {paper.year}
      </p>

      <div className="mt-3 flex gap-2">
        <span className="rounded-md border border-gray-200 px-3 py-1 text-xs text-gray-500">
          DOI: {paper.doi}
        </span>
        <span className="rounded-md border border-gray-200 px-3 py-1 text-xs text-gray-500">
          OpenAlex ID: {paper.openAlexId}
        </span>
      </div>

      <hr className="my-4 border-gray-200" />

      <h2 className="text-lg font-semibold text-gray-800">Abstract</h2>
      <p className="mt-2 text-sm text-gray-700">{paper.abstract}</p>

      <div className="mt-3 flex flex-wrap gap-2">
        {paper.keywords.map((kw) => (
          <span key={kw} className="rounded-full bg-indigo-50 px-3 py-1 text-xs text-indigo-700">
            {kw}
          </span>
        ))}
      </div>

      <hr className="my-4 border-gray-200" />

      <div className="flex items-center justify-between">
        <p className="text-xs text-gray-500">
          {/* BR-32: 1 nút duy nhất - vừa Bookmark, vừa đăng ký nhận NEW_CITATION */}
          Bookmarking this paper also subscribes you to "New Citation" notifications (FR-26).
        </p>

        {bookmarked ? (
          <button
            onClick={() => toggleBookmark(paper)}
            className="shrink-0 rounded-md border border-red-600 px-4 py-2 text-sm text-red-600 hover:bg-red-50"
          >
            Remove Bookmark
          </button>
        ) : (
          <button
            onClick={() => toggleBookmark(paper)}
            className="shrink-0 rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800"
          >
            Bookmark Paper
          </button>
        )}
      </div>
    </div>
  );
};

export default PaperDetailPage;

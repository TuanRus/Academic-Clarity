import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useBookmark } from '../../hooks/useBookmark';
import { getMyFollows, toggleFollow, type FollowedItem } from '../../lib/api/follow';

// LS-05 · Saved Papers Library (Bookmark Manager) - FR-24
// + Danh sách Followed Topics & Journals (chuyển từ Notification Center sang đây).
const SavedLibraryPage = () => {
  const { bookmarkedPapers, toggleBookmark } = useBookmark();

  const [follows, setFollows] = useState<FollowedItem[]>([]);
  useEffect(() => {
    getMyFollows().then(setFollows).catch(() => setFollows([]));
  }, []);

  const onUnfollow = async (f: FollowedItem) => {
    await toggleFollow(f.targetType as 'topic' | 'journal', f.targetId).catch(() => {});
    setFollows((prev) => prev.filter((x) => x.followId !== f.followId));
  };

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
          <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
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
              className="self-start shrink-0 rounded-md border border-red-600 px-3 py-1.5 text-sm text-red-600 hover:bg-red-50"
            >
              Remove Bookmark
            </button>
          </div>
        </div>
      ))}

      {/* Followed Topics & Journals (chuyển từ Notification Center) */}
      <div className="pt-2">
        <h2 className="text-lg font-bold text-gray-900">
          Followed Topics &amp; Journals ({follows.length})
        </h2>
        <p className="text-xs text-gray-500">Topics, journals and authors you follow for new-paper alerts.</p>

        <div className="mt-3 grid gap-2 sm:grid-cols-2">
          {follows.map((f) => (
            <div key={f.followId} className="flex items-center justify-between rounded-lg border border-gray-200 bg-white p-3">
              <div className="min-w-0">
                <span className="rounded-full bg-gray-100 px-2 py-0.5 text-[10px] uppercase text-gray-500">{f.targetType}</span>
                <p className="mt-1 truncate text-sm text-gray-800">{f.name}</p>
              </div>
              <button
                onClick={() => onUnfollow(f)}
                className="shrink-0 rounded-md border border-red-300 px-2 py-1 text-xs text-red-600 hover:bg-red-50"
              >
                Unfollow
              </button>
            </div>
          ))}
          {follows.length === 0 && (
            <p className="text-xs text-gray-400">Not following anything yet.</p>
          )}
        </div>
      </div>
    </div>
  );
};

export default SavedLibraryPage;

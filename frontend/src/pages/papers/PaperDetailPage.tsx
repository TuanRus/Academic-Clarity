import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useBookmark } from '../../hooks/useBookmark';
import { getPaperDetail } from '../../lib/api/mindmap';
import { ApiError } from '../../lib/http';
import type { BookmarkedPaper } from '../../context/BookmarkContext';
import type { PaperDetail } from '../../types/api';

// LS-03 · Paper Detail Screen - FR-23 (+ BR-32: Bookmark = Follow cho ResearchPaper)
// ĐÃ NỐI API thật: GET /api/mindmap/paper/{paperId}.
// Thông tin cốt lõi từ DB; abstract ráp on-demand từ OpenAlex (func ReconstructAbstract của BE).
const PaperDetailPage = () => {
  const { paperId = '' } = useParams();
  const { isBookmarked, toggleBookmark } = useBookmark();

  const [paper, setPaper] = useState<PaperDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!paperId) return;
    setLoading(true);
    setError(null);
    getPaperDetail(paperId)
      .then(setPaper)
      .catch((e) => {
        setPaper(null);
        setError(e instanceof ApiError ? e.message : 'Could not load paper details.');
      })
      .finally(() => setLoading(false));
  }, [paperId]);

  if (loading) {
    return <div className="rounded-xl border border-gray-200 bg-white p-6 text-sm text-gray-400 shadow-sm">Loading…</div>;
  }
  if (error || !paper) {
    return (
      <div className="rounded-xl border border-gray-200 bg-white p-6 text-sm text-red-600 shadow-sm">
        {error ?? 'Paper not found.'}
      </div>
    );
  }

  const bookmark: BookmarkedPaper = {
    paperId: paper.paperId,
    title: paper.title,
    year: paper.publicationYear,
    keywords: paper.keywords,
    citationCount: paper.citationCount,
    sourceUrl: paper.sourceUrl ?? undefined,
  };
  const bookmarked = isBookmarked(paper.paperId);

  // Các trường có thể thiếu (DB/OpenAlex không có) -> note rõ cho người dùng.
  const missing: string[] = [];
  if (!paper.abstract) missing.push('Abstract (OpenAlex does not provide one for this paper)');
  if (paper.authors.length === 0) missing.push('Author list');
  if (!paper.doi) missing.push('DOI');
  if (!paper.journalName) missing.push('Journal');
  if (!paper.topic) missing.push('Topic');
  if (paper.institutions.length === 0) missing.push('Institutions');
  if (!paper.openAccessStatus) missing.push('Open Access status');

  const classification = [
    { label: 'Topic', value: paper.topic },
    { label: 'Subfield', value: paper.subfield },
    { label: 'Field', value: paper.field },
    { label: 'Domain', value: paper.domain },
  ].filter((r) => r.value);

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
      <p className="text-xs font-medium uppercase tracking-wide text-gray-400">Paper Detail</p>
      <h1 className="mt-1 text-2xl font-bold text-gray-900">{paper.title}</h1>

      {paper.authors.length > 0 && (
        <p className="mt-1 text-sm text-gray-700">{paper.authors.join(', ')}</p>
      )}
      <p className="text-sm italic text-gray-500">
        {paper.journalName || 'Unknown journal'}
        {paper.publicationYear ? ` · ${paper.publicationYear}` : ''}
        {paper.quartile ? ` · ${paper.quartile}` : ''}
        {` · ${paper.citationCount} citations`}
      </p>
      {(paper.publisher || paper.impactFactor != null) && (
        <p className="text-xs text-gray-500">
          {paper.publisher ? `Publisher: ${paper.publisher}` : ''}
          {paper.impactFactor != null ? `${paper.publisher ? ' · ' : ''}Impact Factor: ${paper.impactFactor}` : ''}
        </p>
      )}

      <div className="mt-3 flex flex-wrap gap-2">
        {paper.doi && (
          <a
            href={`https://doi.org/${paper.doi}`}
            target="_blank"
            rel="noreferrer"
            className="rounded-md border border-gray-200 px-3 py-1 text-xs text-indigo-700 hover:bg-indigo-50"
          >
            DOI: {paper.doi} ↗
          </a>
        )}
        {paper.openAlexId && (
          <span className="rounded-md border border-gray-200 px-3 py-1 text-xs text-gray-500">
            OpenAlex ID: {paper.openAlexId}
          </span>
        )}
        {paper.sourceUrl && (
          <a
            href={paper.sourceUrl}
            target="_blank"
            rel="noreferrer"
            className="rounded-md border border-gray-200 px-3 py-1 text-xs text-indigo-700 hover:bg-indigo-50"
          >
            View original ↗
          </a>
        )}
      </div>

      <hr className="my-4 border-gray-200" />

      <h2 className="text-lg font-semibold text-gray-800">Abstract</h2>
      {paper.abstract ? (
        <p className="mt-2 whitespace-pre-line text-sm text-gray-700">{paper.abstract}</p>
      ) : (
        <p className="mt-2 text-sm italic text-gray-400">
          OpenAlex does not provide an abstract for this paper.
        </p>
      )}

      {paper.keywords.length > 0 && (
        <div className="mt-3 flex flex-wrap gap-2">
          {paper.keywords.map((kw) => (
            <span key={kw} className="rounded-full bg-indigo-50 px-3 py-1 text-xs text-indigo-700">
              {kw}
            </span>
          ))}
        </div>
      )}

      {(classification.length > 0 || paper.institutions.length > 0 || paper.openAccessStatus) && (
        <>
          <hr className="my-4 border-gray-200" />
          <h2 className="text-lg font-semibold text-gray-800">Classification & Source</h2>
          <dl className="mt-2 grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
            {classification.map((r) => (
              <div key={r.label} className="flex gap-2">
                <dt className="w-24 shrink-0 text-gray-500">{r.label}:</dt>
                <dd className="text-gray-800">{r.value}</dd>
              </div>
            ))}
            {paper.openAccessStatus && (
              <div className="flex gap-2">
                <dt className="w-24 shrink-0 text-gray-500">Open Access:</dt>
                <dd>
                  <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium uppercase text-emerald-700">
                    {paper.openAccessStatus}
                  </span>
                </dd>
              </div>
            )}
          </dl>

          {paper.institutions.length > 0 && (
            <div className="mt-3 flex gap-2 text-sm">
              <span className="w-24 shrink-0 text-gray-500">Institutions:</span>
              <span className="text-gray-800">{paper.institutions.join(' · ')}</span>
            </div>
          )}
        </>
      )}

      {missing.length > 0 && (
        <div className="mt-4 rounded-md border border-amber-200 bg-amber-50 p-3 text-xs text-amber-800">
          <span className="font-semibold">Missing data:</span> {missing.join(' · ')}.
        </div>
      )}

      <hr className="my-4 border-gray-200" />

      <div className="flex items-center justify-between gap-4">
        <p className="text-xs text-gray-500">
          {/* BR-32: 1 nút duy nhất - vừa Bookmark, vừa đăng ký nhận NEW_CITATION */}
          Bookmarking this paper also subscribes you to "New Citation" notifications.
        </p>
        <button
          onClick={() => toggleBookmark(bookmark)}
          className={
            bookmarked
              ? 'shrink-0 rounded-md border border-red-600 px-4 py-2 text-sm text-red-600 hover:bg-red-50'
              : 'shrink-0 rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800'
          }
        >
          {bookmarked ? 'Remove Bookmark' : 'Bookmark Paper'}
        </button>
      </div>
    </div>
  );
};

export default PaperDetailPage;

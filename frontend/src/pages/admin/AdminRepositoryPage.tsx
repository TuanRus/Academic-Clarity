import { useEffect, useState } from 'react';
import AdminModal from '../../components/admin/AdminModal';
import AdminSectionCard from '../../components/admin/AdminSectionCard';
import AdminTable from '../../components/admin/AdminTable';
import { useAdminToast } from '../../context/AdminToastContext';
import {
  getPapers,
  createPaperFromLink,
  createPaper,
  deletePaperApi,
  type RepositoryPaper,
} from '../../lib/api/admin';
import { ApiError } from '../../lib/http';

const AdminRepositoryPage = () => {
  const { showSuccess, showError } = useAdminToast();
  const [papers, setPapers] = useState<RepositoryPaper[]>([]);
  const [query, setQuery] = useState('');

  // Debounced server-side paper search across ENTIRE database
  useEffect(() => {
    const timer = setTimeout(() => {
      getPapers(1, 200, query).then(setPapers).catch(() => setPapers([]));
    }, 300);
    return () => clearTimeout(timer);
  }, [query]);

  // ----- Thêm bài báo (dán link OpenAlex/DOI hoặc nhập tay) -----
  const [showAddModal, setShowAddModal] = useState(false);
  const [addMode, setAddMode] = useState<'link' | 'manual'>('link');
  const [addLink, setAddLink] = useState('');
  const [mTitle, setMTitle] = useState('');
  const [mDoi, setMDoi] = useState('');
  const [mOpenAlexId, setMOpenAlexId] = useState('');
  const [mSourceUrl, setMSourceUrl] = useState('');
  const [mJournal, setMJournal] = useState('');
  const [mYear, setMYear] = useState('');
  const [mPubDate, setMPubDate] = useState('');
  const [mCitations, setMCitations] = useState('');
  const [mAuthors, setMAuthors] = useState('');
  const [mKeywords, setMKeywords] = useState('');
  const [mTopic, setMTopic] = useState('');
  const [adding, setAdding] = useState(false);
  const [addError, setAddError] = useState<string | null>(null);

  const reloadPapers = () => getPapers().then(setPapers).catch(() => { });

  const submitAdd = async () => {
    setAdding(true);
    setAddError(null);
    try {
      if (addMode === 'link') {
        if (!addLink.trim()) {
          setAddError('Please enter an OpenAlex or DOI link.');
          setAdding(false);
          return;
        }
        await createPaperFromLink(addLink.trim());
      } else {
        if (!mTitle.trim()) {
          setAddError('Paper title is required.');
          setAdding(false);
          return;
        }
        await createPaper({
          title: mTitle.trim(),
          doi: mDoi.trim() || undefined,
          openAlexId: mOpenAlexId.trim() || undefined,
          sourceUrl: mSourceUrl.trim() || undefined,
          publicationDate: mPubDate.trim() || undefined,
          publicationYear: mPubDate.trim() ? new Date(mPubDate).getFullYear() : (mYear.trim() ? Number(mYear) : undefined),
          citationCount: mCitations.trim() ? Number(mCitations) : 0,
          journalName: mJournal.trim() || undefined,
          topic: mTopic.trim() || undefined,
          authors: mAuthors.split(/[,;]/).map((s) => s.trim()).filter(Boolean),
          keywords: mKeywords.split(/[,;]/).map((s) => s.trim()).filter(Boolean),
        });
      }
      showSuccess('Paper added successfully.');
      setShowAddModal(false);
      setAddLink(''); setMTitle(''); setMJournal(''); setMYear(''); setMAuthors(''); setMKeywords(''); setMTopic('');
      setMDoi(''); setMOpenAlexId(''); setMSourceUrl(''); setMPubDate(''); setMCitations('');
      setAddError(null);
      reloadPapers();
      setTimeout(() => {
        window.location.reload();
      }, 500);
    } catch (e) {
      const errorMsg = e instanceof ApiError
        ? e.message
        : 'Warning: Failed to add paper. This paper may already exist in the system (duplicate title, DOI, or OpenAlex ID).';
      setAddError(errorMsg);
      showError(errorMsg);
    } finally {
      setAdding(false);
    }
  };

  const handleDeletePaper = async (paperId: string, title: string) => {
    if (!window.confirm(`Are you sure you want to delete paper:\n"${title}"?`)) return;
    try {
      await deletePaperApi(paperId);
      showSuccess(`Paper deleted successfully: "${title}"`);
      reloadPapers();
    } catch (e) {
      showError(`Failed to delete paper: "${title}"`);
    }
  };

  return (
    <div className="space-y-5">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight text-slate-950">
            Article Repository
          </h1>
          <p className="mt-1 text-xs text-slate-500">
            Manage OpenAlex paper metadata, journals and citations.
          </p>
        </div>

        <div className="flex gap-3">
          <button
            onClick={() => { setShowAddModal(true); setAddError(null); }}
            className="rounded-md bg-[#4338ca] hover:bg-[#3730a3] px-4 py-2 text-xs font-bold text-white"
          >
            + Add Paper
          </button>
        </div>
      </div>

      <AdminSectionCard
        title="Research Papers"
        subtitle="Manage imported paper metadata from OpenAlex."
        action={
          <input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Search database by title, author, journal, DOI..."
            className="w-80 rounded-md border border-slate-200 px-3 py-2 text-xs outline-none focus:border-[#0b6fb8]"
          />
        }
      >
        <AdminTable headers={['Paper ID', 'Title', 'Authors', 'Journal', 'Year', 'Citations', 'DOI', 'Actions']}>
          {papers.map((paper) => (
            <tr key={paper.id} className="hover:bg-slate-50">
              <td className="px-5 py-4 font-bold text-slate-700">
                {paper.id}
              </td>

              <td className="max-w-sm px-5 py-4">
                <p className="font-semibold text-slate-900">
                  {paper.title}
                </p>
              </td>

              <td className="px-5 py-4 text-sm text-slate-600">
                {paper.authors}
              </td>

              <td className="px-5 py-4 text-slate-600">
                {paper.journal}
              </td>

              <td className="px-5 py-4 font-semibold text-slate-700">
                {paper.year}
              </td>

              <td className="px-5 py-4 font-bold text-slate-900">
                {paper.citations}
              </td>

              <td className="px-5 py-4">
                <a
                  href={`https://doi.org/${paper.doi}`}
                  target="_blank"
                  rel="noreferrer"
                  className="text-xs font-bold text-[#0b6fb8] hover:underline"
                >
                  {paper.doi}
                </a>
              </td>

              <td className="px-5 py-4">
                <button
                  onClick={() => handleDeletePaper(paper.id, paper.title)}
                  className="text-xs font-bold text-rose-600 hover:text-rose-800 hover:underline"
                >
                  Delete
                </button>
              </td>
            </tr>
          ))}
        </AdminTable>
      </AdminSectionCard>

      <AdminModal
        open={showAddModal}
        title="Add Scientific Paper"
        subtitle="Paste an OpenAlex/DOI link to auto-fetch metadata, or enter details manually."
        onClose={() => setShowAddModal(false)}
        footer={
          <>
            <button onClick={() => setShowAddModal(false)} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700">Cancel</button>
            <button onClick={submitAdd} disabled={adding} className="rounded-md bg-[#4338ca] px-4 py-2 text-xs font-bold text-white disabled:opacity-50">
              {adding ? 'Adding…' : 'Add Paper'}
            </button>
          </>
        }
      >
        <div className="space-y-3">
          {addError && (
            <div className="flex items-start gap-2 rounded-md border border-amber-300 bg-amber-50 p-3 text-xs font-semibold text-amber-900">
              <span className="text-base leading-none text-amber-600">⚠️</span>
              <div className="flex-1">{addError}</div>
              <button type="button" onClick={() => setAddError(null)} className="font-bold text-amber-500 hover:text-amber-800">×</button>
            </div>
          )}

          <div className="flex gap-2">
            <button onClick={() => { setAddMode('link'); setAddError(null); }} className={`rounded-md px-3 py-1.5 text-xs font-bold ${addMode === 'link' ? 'bg-[#4338ca] text-white' : 'bg-slate-100 text-slate-600'}`}>From link (OpenAlex/DOI)</button>
            <button onClick={() => { setAddMode('manual'); setAddError(null); }} className={`rounded-md px-3 py-1.5 text-xs font-bold ${addMode === 'manual' ? 'bg-[#4338ca] text-white' : 'bg-slate-100 text-slate-600'}`}>Manual</button>
          </div>

          {addMode === 'link' ? (
            <div>
              <label className="mb-1 block text-xs font-bold text-slate-700">Paper link</label>
              <input
                value={addLink}
                onChange={(e) => setAddLink(e.target.value)}
                placeholder="https://openalex.org/W… or https://doi.org/10.… or a DOI"
                className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]"
              />
              <p className="mt-1 text-xs text-slate-400">The backend auto-fetches title, authors, journal, year and keywords from OpenAlex.</p>
            </div>
          ) : (
            <div className="space-y-2">
              <input value={mTitle} onChange={(e) => setMTitle(e.target.value)} placeholder="Paper title (required)" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]" />

              <div className="grid grid-cols-2 gap-2">
                <input value={mDoi} onChange={(e) => setMDoi(e.target.value)} placeholder="DOI (e.g. 10.1016/j.artint...)" className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]" />
                <input value={mOpenAlexId} onChange={(e) => setMOpenAlexId(e.target.value)} placeholder="OpenAlex ID (e.g. W3123456789)" className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]" />
              </div>

              <input value={mSourceUrl} onChange={(e) => setMSourceUrl(e.target.value)} placeholder="Source URL (Original paper link)" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]" />

              <div className="grid grid-cols-4 gap-2">
                <input value={mJournal} onChange={(e) => setMJournal(e.target.value)} placeholder="Journal" className="col-span-1 rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]" />
                <input value={mYear} onChange={(e) => setMYear(e.target.value)} placeholder="Year (e.g. 2025)" inputMode="numeric" className="col-span-1 rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]" />
                <input type="date" value={mPubDate} onChange={(e) => setMPubDate(e.target.value)} placeholder="Exact Date" className="col-span-1 rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]" />
                <input value={mCitations} onChange={(e) => setMCitations(e.target.value)} placeholder="Citations (e.g. 25520)" inputMode="numeric" className="col-span-1 rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]" />
              </div>

              <input value={mTopic} onChange={(e) => setMTopic(e.target.value)} placeholder="Topic (used for follower notifications)" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]" />
              <input value={mAuthors} onChange={(e) => setMAuthors(e.target.value)} placeholder="Authors (comma-separated)" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]" />
              <input value={mKeywords} onChange={(e) => setMKeywords(e.target.value)} placeholder="Keywords (comma-separated)" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]" />
            </div>
          )}
        </div>
      </AdminModal>
    </div>
  );
};

export default AdminRepositoryPage;

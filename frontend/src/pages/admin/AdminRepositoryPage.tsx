import { useEffect, useState } from 'react';
import AdminModal from '../../components/admin/AdminModal';
import AdminBadge from '../../components/admin/AdminBadge';
import AdminSectionCard from '../../components/admin/AdminSectionCard';
import AdminTable from '../../components/admin/AdminTable';
import AdminToast from '../../components/admin/AdminToast';
import {
  getPapers,
  getKeywords,
  createPaperFromLink,
  createPaper,
  deletePaperApi,
  createKeywordApi,
  deleteKeywordApi,
  type RepositoryCategory,
  type RepositoryPaper,
} from '../../lib/api/admin';
import { suggestKeywords } from '../../lib/api/mindmap';
import { ApiError } from '../../lib/http';

const AdminRepositoryPage = () => {
  const [activeTab, setActiveTab] = useState<'papers' | 'keywords'>('papers');
  const [keywords, setKeywords] = useState<RepositoryCategory[]>([]);
  const [papers, setPapers] = useState<RepositoryPaper[]>([]);
  const [query, setQuery] = useState('');
  const [keywordQuery, setKeywordQuery] = useState('');
  const [suggestions, setSuggestions] = useState<string[]>([]);
  const [showSuggestions, setShowSuggestions] = useState(false);

  // Debounced server-side paper search across ENTIRE database
  useEffect(() => {
    const timer = setTimeout(() => {
      getPapers(1, 200, query).then(setPapers).catch(() => setPapers([]));
    }, 300);
    return () => clearTimeout(timer);
  }, [query]);

  // Debounced server-side keyword search across ENTIRE database
  useEffect(() => {
    const timer = setTimeout(() => {
      getKeywords(1, 100, keywordQuery).then(setKeywords).catch(() => setKeywords([]));
    }, 300);
    return () => clearTimeout(timer);
  }, [keywordQuery]);

  // Autocomplete keyword suggestions API call (reusing suggestKeywords)
  useEffect(() => {
    const term = query.trim();
    if (term.length < 2) {
      setSuggestions([]);
      return;
    }
    const id = setTimeout(() => {
      suggestKeywords(term, 8)
        .then(setSuggestions)
        .catch(() => setSuggestions([]));
    }, 200);
    return () => clearTimeout(id);
  }, [query]);
  const [showKeywordModal, setShowKeywordModal] = useState(false);
  const [keywordName, setKeywordName] = useState('');
  const [keywordDescription, setKeywordDescription] = useState('');
  const [toast, setToast] = useState<string | null>(null);

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
      setToast('Paper added successfully.');
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
      setToast(errorMsg);
    } finally {
      setAdding(false);
    }
  };

  const filteredPapers = papers.filter((paper) =>
    `${paper.id} ${paper.title} ${paper.authors} ${paper.doi} ${paper.journal} ${paper.year} ${paper.citations}`
      .toLowerCase()
      .includes(query.toLowerCase()),
  );

  const filteredKeywords = keywords.filter((keyword) =>
    `${keyword.id} ${keyword.name} ${keyword.description} ${keyword.fields} ${keyword.status}`
      .toLowerCase()
      .includes(keywordQuery.trim().toLowerCase()),
  );


  const handleDeletePaper = async (paperId: string, title: string) => {
    if (!window.confirm(`Are you sure you want to delete paper:\n"${title}"?`)) return;
    try {
      await deletePaperApi(paperId);
      setToast(`Paper deleted successfully: "${title}"`);
      reloadPapers();
    } catch (e) {
      setToast(`Failed to delete paper: "${title}"`);
    }
  };

  const createKeyword = async () => {
    if (!keywordName.trim()) {
      setToast('Keyword name is required.');
      return;
    }
    try {
      await createKeywordApi(keywordName.trim());
      setKeywordName('');
      setKeywordDescription('');
      setShowKeywordModal(false);
      setToast('New keyword created successfully.');
      getKeywords().then(setKeywords).catch(() => { });
    } catch (e) {
      setToast(e instanceof ApiError ? e.message : 'Failed to create keyword (may already exist).');
    }
  };

  const handleDeleteKeyword = async (keywordId: string, name: string) => {
    if (!window.confirm(`Are you sure you want to delete keyword:\n"${name}"?`)) return;
    try {
      await deleteKeywordApi(keywordId);
      setToast('Keyword deleted successfully.');
      getKeywords().then(setKeywords).catch(() => { });
    } catch (e) {
      setToast('Failed to delete keyword.');
    }
  };

  return (
    <div className="space-y-5">
      <AdminToast message={toast} onClose={() => setToast(null)} />

      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight text-slate-950">
            Article Repository
          </h1>
          <p className="mt-1 text-xs text-slate-500">
            Manage OpenAlex paper metadata, journals, citations and research keywords.
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

      <div className="border-b border-slate-200">
        <div className="flex gap-8 text-sm font-bold text-slate-500">
          <button
            onClick={() => setActiveTab('papers')}
            className={`pb-3 hover:text-[#0b6fb8] ${activeTab === 'papers' ? 'border-b-2 border-[#0b6fb8] text-[#062b4f]' : ''
              }`}
          >
            Research Papers
          </button>

          <button
            onClick={() => setActiveTab('keywords')}
            className={`pb-3 hover:text-[#0b6fb8] ${activeTab === 'keywords' ? 'border-b-2 border-[#0b6fb8] text-[#062b4f]' : ''
              }`}
          >
            Keywords
          </button>
        </div>
      </div>

      {activeTab === 'papers' ? (
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
      ) : (
        <AdminSectionCard
          title="Keyword Registry"
          subtitle="Manage research keywords used for trend analysis and search suggestions."
          action={
            <div className="flex items-center gap-3">
              <input
                value={keywordQuery}
                onChange={(event) => setKeywordQuery(event.target.value)}
                placeholder="Search keywords in database..."
                className="w-72 rounded-md border border-slate-200 px-3 py-2 text-xs outline-none focus:border-[#0b6fb8]"
              />

              <button
                type="button"
                onClick={() => setShowKeywordModal(true)}
                className="whitespace-nowrap rounded-md bg-[#4338ca] px-4 py-2 text-xs font-bold text-white hover:bg-[#3730a3]"
              >
                + New Keyword
              </button>
            </div>
          }
        >
          <AdminTable headers={['Keyword ID', 'Keyword Name', 'Description', 'Related Papers', 'Status', 'Actions']}>
            {keywords.map((keyword) => (
              <tr key={keyword.id} className={`hover:bg-slate-50 ${keyword.status === 'DISMISSED' ? 'opacity-50' : ''}`} >
                <td className="px-5 py-5 font-bold text-slate-500">{keyword.id}</td>

                <td className="px-5 py-5">
                  <p className="font-bold text-slate-900">{keyword.name}</p>
                </td>

                <td className="px-5 py-5">
                  <p className="max-w-md text-xs text-slate-500">{keyword.description}</p>
                </td>

                <td className="px-5 py-5">
                  <span className="rounded bg-slate-100 px-3 py-1 text-xs font-bold text-slate-600">
                    {keyword.fields} Papers
                  </span>
                </td>
                <td className="px-5 py-5">
                  <AdminBadge status={keyword.status} />
                </td>

                <td className="px-5 py-5">
                  <button
                    onClick={() => handleDeleteKeyword(keyword.id, keyword.name)}
                    className="text-xs font-bold text-rose-600 hover:text-rose-800 hover:underline"
                  >
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </AdminTable>
        </AdminSectionCard>
      )}

      <AdminModal
        open={showKeywordModal}
        title="Create New Keyword"
        subtitle="Add a keyword for trend tracking and search suggestion."
        onClose={() => setShowKeywordModal(false)}
        footer={
          <>
            <button
              onClick={() => setShowKeywordModal(false)}
              className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700"
            >
              Cancel
            </button>

            <button
              onClick={createKeyword}
              className="rounded-md bg-[#4338ca] px-4 py-2 text-xs font-bold text-white"
            >
              Create Keyword
            </button>
          </>
        }
      >
        <div className="space-y-4">
          <label className="block text-xs font-bold text-slate-700" htmlFor="keyword-name">
            Keyword name
          </label>

          <input
            id="keyword-name"
            value={keywordName}
            onChange={(event) => setKeywordName(event.target.value)}
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]"
            placeholder="Example: Artificial Intelligence"
          />

          <label className="block text-xs font-bold text-slate-700" htmlFor="keyword-description">
            Description
          </label>

          <textarea
            id="keyword-description"
            value={keywordDescription}
            onChange={(event) => setKeywordDescription(event.target.value)}
            rows={3}
            className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]"
            placeholder="Short keyword description..."
          />
        </div>
      </AdminModal>

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
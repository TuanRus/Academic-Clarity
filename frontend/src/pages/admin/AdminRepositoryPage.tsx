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
  type RepositoryCategory,
  type RepositoryPaper,
} from '../../lib/api/admin';
import { ApiError } from '../../lib/http';

const AdminRepositoryPage = () => {
  const [activeTab, setActiveTab] = useState<'papers' | 'keywords'>('papers');
  const [keywords, setKeywords] = useState<RepositoryCategory[]>([]);
  const [papers, setPapers] = useState<RepositoryPaper[]>([]);
  useEffect(() => {
    getPapers().then(setPapers).catch(() => setPapers([]));
    getKeywords().then(setKeywords).catch(() => setKeywords([]));
  }, []);
  const [query, setQuery] = useState('');
  const [keywordQuery, setKeywordQuery] = useState('');
  const [showKeywordModal, setShowKeywordModal] = useState(false);
  const [keywordName, setKeywordName] = useState('');
  const [keywordDescription, setKeywordDescription] = useState('');
  const [toast, setToast] = useState<string | null>(null);

  // ----- Thêm bài báo (dán link OpenAlex/DOI hoặc nhập tay) -----
  const [showAddModal, setShowAddModal] = useState(false);
  const [addMode, setAddMode] = useState<'link' | 'manual'>('link');
  const [addLink, setAddLink] = useState('');
  const [mTitle, setMTitle] = useState('');
  const [mJournal, setMJournal] = useState('');
  const [mYear, setMYear] = useState('');
  const [mAuthors, setMAuthors] = useState('');
  const [mKeywords, setMKeywords] = useState('');
  const [mTopic, setMTopic] = useState('');
  const [adding, setAdding] = useState(false);

  const reloadPapers = () => getPapers().then(setPapers).catch(() => { });

  const submitAdd = async () => {
    setAdding(true);
    try {
      if (addMode === 'link') {
        if (!addLink.trim()) { setToast('Enter an OpenAlex or DOI link.'); setAdding(false); return; }
        await createPaperFromLink(addLink.trim());
      } else {
        if (!mTitle.trim()) { setToast('Paper title is required.'); setAdding(false); return; }
        await createPaper({
          title: mTitle.trim(),
          journalName: mJournal.trim() || undefined,
          publicationYear: mYear.trim() ? Number(mYear) : undefined,
          topic: mTopic.trim() || undefined,
          authors: mAuthors.split(',').map((s) => s.trim()).filter(Boolean),
          keywords: mKeywords.split(',').map((s) => s.trim()).filter(Boolean),
        });
      }
      setToast('Paper added successfully.');
      setShowAddModal(false);
      setAddLink(''); setMTitle(''); setMJournal(''); setMYear(''); setMAuthors(''); setMKeywords(''); setMTopic('');
      reloadPapers();
    } catch (e) {
      setToast(e instanceof ApiError ? e.message : 'Failed to add paper (invalid link or duplicate).');
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

  const exportRepository = () => {
    const content = papers
      .map((paper) => `${paper.id} ${paper.title} ${paper.authors} ${paper.doi} ${paper.journal} ${paper.year} ${paper.citations}`)
      .join('\n');

    const blob = new Blob(
      [`ID,Title,Journal,Year,Citations,DOI\n${content}`],
      { type: 'text/csv;charset=utf-8;' },
    );

    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');

    anchor.href = url;
    anchor.download = 'article-repository.csv';
    anchor.click();

    URL.revokeObjectURL(url);
    setToast('Article repository exported.');
  };

  const createKeyword = () => {
    if (!keywordName.trim()) {
      setToast('Keyword name is required.');
      return;
    }

    const nextKeyword: RepositoryCategory = {
      id: `KEY-${String(keywords.length + 1).padStart(3, '0')}`,
      name: keywordName.trim(),
      description: keywordDescription.trim() || 'New keyword used for trend tracking',
      fields: 0,
      status: 'DRAFT',
    };

    setKeywords((current) => [...current, nextKeyword]);
    setKeywordName('');
    setKeywordDescription('');
    setShowKeywordModal(false);
    setToast('New keyword created.');
  };

  const toggleKeywordStatus = (keywordId: string) => {
    setKeywords((current) =>
      current.map((keyword) =>
        keyword.id === keywordId
          ? {
            ...keyword,
            status: keyword.status === 'DISMISSED' ? 'ACTIVE' : 'DISMISSED',
          }
          : keyword,
      ),
    );

    setToast('Keyword status updated.');
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
            onClick={exportRepository}
            className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-[#0b6fb8]"
          >
            ⇩ Export
          </button>

          <button
            onClick={() => setShowAddModal(true)}
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
              placeholder="Filter by title, journal, year, citation or DOI..."
              className="w-72 rounded-md border border-slate-200 px-3 py-2 text-xs outline-none focus:border-[#0b6fb8]"
            />
          }
        >
          <AdminTable headers={['Paper ID', 'Title', 'Authors', 'Journal', 'Year', 'Citations', 'DOI']}>
            {filteredPapers.map((paper) => (
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
                placeholder="Filter by keyword name, ID, description or status..."
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
            {filteredKeywords.map((keyword) => (
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
                    onClick={() => toggleKeywordStatus(keyword.id)}
                    className={
                      keyword.status === 'DISMISSED'
                        ? 'text-xs font-bold text-emerald-700 hover:underline'
                        : 'text-xs font-bold text-orange-700 hover:underline'
                    }
                  >
                    {keyword.status === 'DISMISSED' ? 'Restore' : 'Disable'}
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
          <div className="flex gap-2">
            <button onClick={() => setAddMode('link')} className={`rounded-md px-3 py-1.5 text-xs font-bold ${addMode === 'link' ? 'bg-[#4338ca] text-white' : 'bg-slate-100 text-slate-600'}`}>From link (OpenAlex/DOI)</button>
            <button onClick={() => setAddMode('manual')} className={`rounded-md px-3 py-1.5 text-xs font-bold ${addMode === 'manual' ? 'bg-[#4338ca] text-white' : 'bg-slate-100 text-slate-600'}`}>Manual</button>
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
                <input value={mJournal} onChange={(e) => setMJournal(e.target.value)} placeholder="Journal" className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]" />
                <input value={mYear} onChange={(e) => setMYear(e.target.value)} placeholder="Year (e.g. 2025)" inputMode="numeric" className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]" />
              </div>
              <input value={mTopic} onChange={(e) => setMTopic(e.target.value)} placeholder="Topic (chủ đề — dùng cho thông báo follower)" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]" />
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
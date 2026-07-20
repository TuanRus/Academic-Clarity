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

  // Ô CSV phải bọc ngoặc kép khi chứa dấu phẩy/ngoặc kép/xuống dòng, nếu không cột sẽ bị vỡ
  const csvCell = (value: string | number) => {
    const s = String(value ?? '');
    return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };

  const exportRepository = () => {
    const header = ['Paper ID', 'Title', 'Authors', 'Journal', 'Year', 'Citations', 'DOI'];
    const rows = papers.map((paper) =>
      [paper.id, paper.title, paper.authors, paper.journal, paper.year, paper.citations, paper.doi]
        .map(csvCell)
        .join(','),
    );

    // BOM để Excel nhận đúng UTF-8 (tên bài báo/tạp chí có ký tự ngoài ASCII)
    const blob = new Blob(
      ['\uFEFF' + [header.join(','), ...rows].join('\r\n')],
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
          <h1 className="text-2xl font-bold tracking-tight text-gray-900">
            Article Repository
          </h1>
          <p className="mt-1 text-xs text-gray-500">
            Manage OpenAlex paper metadata, journals, citations and research keywords.
          </p>
        </div>

        <div className="flex gap-3">
          <button
            onClick={exportRepository}
            className="rounded-md border border-gray-300 bg-white px-4 py-2 text-xs font-bold text-indigo-700"
          >
            ⇩ Export
          </button>

          <button
            onClick={() => setShowAddModal(true)}
            className="rounded-md bg-indigo-700 hover:bg-indigo-800 px-4 py-2 text-xs font-bold text-white"
          >
            + Add Paper
          </button>
        </div>
      </div>

      <div className="border-b border-gray-200">
        <div className="flex gap-8 text-sm font-bold text-gray-500">
          <button
            onClick={() => setActiveTab('papers')}
            className={`pb-3 hover:text-indigo-700 ${activeTab === 'papers' ? 'border-b-2 border-indigo-700 text-indigo-800' : ''
              }`}
          >
            Research Papers
          </button>

          <button
            onClick={() => setActiveTab('keywords')}
            className={`pb-3 hover:text-indigo-700 ${activeTab === 'keywords' ? 'border-b-2 border-indigo-700 text-indigo-800' : ''
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
              className="w-72 rounded-md border border-gray-200 px-3 py-2 text-xs outline-none focus:border-indigo-700"
            />
          }
        >
          <AdminTable headers={['Paper ID', 'Title', 'Authors', 'Journal', 'Year', 'Citations', 'DOI']}>
            {filteredPapers.map((paper) => (
              <tr key={paper.id} className="hover:bg-gray-50">
                <td className="px-5 py-4 font-bold text-gray-700">
                  {paper.id}
                </td>

                <td className="max-w-sm px-5 py-4">
                  <p className="font-semibold text-gray-900">
                    {paper.title}
                  </p>
                </td>

                <td className="px-5 py-4 text-sm text-gray-600">
                  {paper.authors}
                </td>

                <td className="px-5 py-4 text-gray-600">
                  {paper.journal}
                </td>

                <td className="px-5 py-4 font-semibold text-gray-700">
                  {paper.year}
                </td>

                <td className="px-5 py-4 font-bold text-gray-900">
                  {paper.citations}
                </td>

                <td className="px-5 py-4">
                  <a
                    href={`https://doi.org/${paper.doi}`}
                    target="_blank"
                    rel="noreferrer"
                    className="text-xs font-bold text-indigo-700 hover:underline"
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
                className="w-72 rounded-md border border-gray-200 px-3 py-2 text-xs outline-none focus:border-indigo-700"
              />

              <button
                type="button"
                onClick={() => setShowKeywordModal(true)}
                className="whitespace-nowrap rounded-md bg-indigo-700 px-4 py-2 text-xs font-bold text-white hover:bg-indigo-800"
              >
                + New Keyword
              </button>
            </div>
          }
        >
          <AdminTable headers={['Keyword ID', 'Keyword Name', 'Description', 'Related Papers', 'Status', 'Actions']}>
            {filteredKeywords.map((keyword) => (
              <tr key={keyword.id} className={`hover:bg-gray-50 ${keyword.status === 'DISMISSED' ? 'opacity-50' : ''}`} >
                <td className="px-5 py-5 font-bold text-gray-500">{keyword.id}</td>

                <td className="px-5 py-5">
                  <p className="font-bold text-gray-900">{keyword.name}</p>
                </td>

                <td className="px-5 py-5">
                  <p className="max-w-md text-xs text-gray-500">{keyword.description}</p>
                </td>

                <td className="px-5 py-5">
                  <span className="rounded bg-gray-100 px-3 py-1 text-xs font-bold text-gray-600">
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
              className="rounded-md border border-gray-300 bg-white px-4 py-2 text-xs font-bold text-gray-700"
            >
              Cancel
            </button>

            <button
              onClick={createKeyword}
              className="rounded-md bg-indigo-700 px-4 py-2 text-xs font-bold text-white"
            >
              Create Keyword
            </button>
          </>
        }
      >
        <div className="space-y-4">
          <label className="block text-xs font-bold text-gray-700" htmlFor="keyword-name">
            Keyword name
          </label>

          <input
            id="keyword-name"
            value={keywordName}
            onChange={(event) => setKeywordName(event.target.value)}
            className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-indigo-700"
            placeholder="Example: Artificial Intelligence"
          />

          <label className="block text-xs font-bold text-gray-700" htmlFor="keyword-description">
            Description
          </label>

          <textarea
            id="keyword-description"
            value={keywordDescription}
            onChange={(event) => setKeywordDescription(event.target.value)}
            rows={3}
            className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-indigo-700"
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
            <button onClick={() => setShowAddModal(false)} className="rounded-md border border-gray-300 bg-white px-4 py-2 text-xs font-bold text-gray-700">Cancel</button>
            <button onClick={submitAdd} disabled={adding} className="rounded-md bg-indigo-700 px-4 py-2 text-xs font-bold text-white disabled:opacity-50">
              {adding ? 'Adding…' : 'Add Paper'}
            </button>
          </>
        }
      >
        <div className="space-y-3">
          <div className="flex gap-2">
            <button onClick={() => setAddMode('link')} className={`rounded-md px-3 py-1.5 text-xs font-bold ${addMode === 'link' ? 'bg-indigo-700 text-white' : 'bg-gray-100 text-gray-600'}`}>From link (OpenAlex/DOI)</button>
            <button onClick={() => setAddMode('manual')} className={`rounded-md px-3 py-1.5 text-xs font-bold ${addMode === 'manual' ? 'bg-indigo-700 text-white' : 'bg-gray-100 text-gray-600'}`}>Manual</button>
          </div>

          {addMode === 'link' ? (
            <div>
              <label className="mb-1 block text-xs font-bold text-gray-700">Paper link</label>
              <input
                value={addLink}
                onChange={(e) => setAddLink(e.target.value)}
                placeholder="https://openalex.org/W… or https://doi.org/10.… or a DOI"
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-indigo-700"
              />
              <p className="mt-1 text-xs text-gray-400">The backend auto-fetches title, authors, journal, year and keywords from OpenAlex.</p>
            </div>
          ) : (
            <div className="space-y-2">
              <input value={mTitle} onChange={(e) => setMTitle(e.target.value)} placeholder="Paper title (required)" className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-indigo-700" />
              <div className="grid grid-cols-2 gap-2">
                <input value={mJournal} onChange={(e) => setMJournal(e.target.value)} placeholder="Journal" className="rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-indigo-700" />
                <input value={mYear} onChange={(e) => setMYear(e.target.value)} placeholder="Year (e.g. 2025)" inputMode="numeric" className="rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-indigo-700" />
              </div>
              <input value={mTopic} onChange={(e) => setMTopic(e.target.value)} placeholder="Topic (chủ đề — dùng cho thông báo follower)" className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-indigo-700" />
              <input value={mAuthors} onChange={(e) => setMAuthors(e.target.value)} placeholder="Authors (comma-separated)" className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-indigo-700" />
              <input value={mKeywords} onChange={(e) => setMKeywords(e.target.value)} placeholder="Keywords (comma-separated)" className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-indigo-700" />
            </div>
          )}
        </div>
      </AdminModal>
    </div>
  );
};

export default AdminRepositoryPage;
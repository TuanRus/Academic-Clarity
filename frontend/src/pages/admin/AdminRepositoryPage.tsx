import { useState, useEffect } from 'react';
import AdminBadge from '../../components/admin/AdminBadge';
import AdminMetricCard from '../../components/admin/AdminMetricCard';
import AdminModal from '../../components/admin/AdminModal';
import AdminSectionCard from '../../components/admin/AdminSectionCard';
import AdminTable from '../../components/admin/AdminTable';
import AdminToast from '../../components/admin/AdminToast';
import {
  getPapers,
  type RepositoryAnomaly,
  type RepositoryCategory,
  type RepositoryPaper,
} from '../../lib/api/admin';

const anomalyTone = {
  orange: 'border-orange-400',
  red: 'border-red-500',
};

const AdminRepositoryPage = () => {
  const [activeTab, setActiveTab] = useState<'papers' | 'ontology'>('papers');
  // Ontology categories & anomalies chưa có endpoint BE → để trống.
  const [categories, setCategories] = useState<RepositoryCategory[]>([]);
  const [anomalies, setAnomalies] = useState<RepositoryAnomaly[]>([]);
  const [papers, setPapers] = useState<RepositoryPaper[]>([]);

  useEffect(() => {
    getPapers().then(setPapers).catch(() => setPapers([]));
  }, []);
  const [query, setQuery] = useState('');
  const [selectedPaper, setSelectedPaper] = useState<RepositoryPaper | null>(null);
  const [showConceptModal, setShowConceptModal] = useState(false);
  const [conceptName, setConceptName] = useState('');
  const [conceptDescription, setConceptDescription] = useState('');
  const [toast, setToast] = useState<string | null>(null);

  const filteredPapers = papers.filter((paper) => `${paper.title} ${paper.doi} ${paper.journal}`.toLowerCase().includes(query.toLowerCase()));

  const exportRepository = () => {
    const content = papers.map((paper) => `${paper.id},${paper.title},${paper.doi},${paper.journal},${paper.year},${paper.citations},${paper.status}`).join('\n');
    const blob = new Blob([`ID,Title,DOI,Journal,Year,Citations,Status\n${content}`], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'article-repository.csv';
    anchor.click();
    URL.revokeObjectURL(url);
    setToast('Article repository exported.');
  };

  const ingestPaper = () => {
    const nextPaper: RepositoryPaper = {
      id: `AC-W${Math.floor(Math.random() * 90000 + 10000)}`,
      title: 'Newly Ingested OpenAlex Metadata Record',
      doi: `10.5555/demo.${Date.now().toString().slice(-5)}`,
      journal: 'OpenAlex Imported Source',
      year: 2026,
      citations: 0,
      status: 'DRAFT',
    };
    setPapers((current) => [nextPaper, ...current]);
    setActiveTab('papers');
    setToast('New OpenAlex metadata record ingested into Research Papers.');
  };

  const handleAnomalyAction = (anomaly: RepositoryAnomaly) => {
    if (anomaly.action === 'Auto-Fill') {
      setAnomalies((current) => current.filter((item) => item.id !== anomaly.id));
      setPapers((current) => current.map((paper) => (paper.title.startsWith('Advanced Neural') ? { ...paper, status: 'VERIFIED' } : paper)));
      setToast(`${anomaly.title} abstract auto-filled and marked verified.`);
      return;
    }

    setAnomalies((current) => current.map((item) => (item.id === anomaly.id ? { ...item, status: 'REVIEWING' } : item)));
    setToast(`${anomaly.title} moved to duplicate review.`);
  };

  const dismissAnomaly = (anomalyId: string) => {
    setAnomalies((current) => current.filter((item) => item.id !== anomalyId));
    setToast('Anomaly dismissed from cleansing queue.');
  };

  const verifyPaper = (paperId: string) => {
    setPapers((current) => current.map((paper) => (paper.id === paperId ? { ...paper, status: 'VERIFIED' } : paper)));
    setToast(`${paperId} has been marked VERIFIED.`);
  };

  const createConcept = () => {
    if (!conceptName.trim()) {
      setToast('Concept name is required.');
      return;
    }

    const nextCategory: RepositoryCategory = {
      id: `CAT-${String(categories.length + 1).padStart(3, '0')}`,
      name: conceptName.trim(),
      description: conceptDescription.trim() || 'New ontology concept awaiting metadata mapping',
      fields: 0,
      status: 'DRAFT',
    };
    setCategories((current) => [...current, nextCategory]);
    setConceptName('');
    setConceptDescription('');
    setShowConceptModal(false);
    setToast('New ontology concept created as DRAFT.');
  };

  return (
    <div className="space-y-5">
      <AdminToast message={toast} onClose={() => setToast(null)} />

      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight text-slate-950">Article Repository</h1>
          <p className="mt-1 text-xs text-slate-500">Validate imported metadata, ontology categories and cleansing queue.</p>
        </div>
        <div className="flex gap-3">
          <button onClick={exportRepository} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-[#0b6fb8]">⇩ Export</button>
          <button onClick={ingestPaper} className="rounded-md bg-[#062b4f] px-4 py-2 text-xs font-bold text-white">Ingest Now</button>
        </div>
      </div>

      <div className="grid gap-5 lg:grid-cols-2">
        <AdminMetricCard label="Total Articles Verified" value={`${papers.filter((paper) => paper.status === 'VERIFIED').length}`} helper="Verified metadata records" icon="▣" accent="slate" />
        <AdminMetricCard label="Data Health Score" value={`${Math.round(((papers.length - anomalies.length) / Math.max(papers.length, 1)) * 100)}%`} helper="Metadata completeness" icon="🛡" accent="blue" />
      </div>

      <div className="border-b border-slate-200">
        <div className="flex gap-8 text-sm font-bold text-slate-500">
          <button onClick={() => setActiveTab('papers')} className={`pb-3 hover:text-[#0b6fb8] ${activeTab === 'papers' ? 'border-b-2 border-[#0b6fb8] text-[#062b4f]' : ''}`}>Research Papers</button>
          <button onClick={() => setActiveTab('ontology')} className={`pb-3 hover:text-[#0b6fb8] ${activeTab === 'ontology' ? 'border-b-2 border-[#0b6fb8] text-[#062b4f]' : ''}`}>Ontology & Categories</button>
        </div>
      </div>

      <div className="grid gap-5 lg:grid-cols-[1fr_310px]">
        {activeTab === 'papers' ? (
          <AdminSectionCard
            title="Research Papers"
            subtitle="Imported metadata records from OpenAlex, Semantic Scholar or Crossref."
            action={<input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Filter papers..." className="rounded-md border border-slate-200 px-3 py-2 text-xs outline-none focus:border-[#0b6fb8]" />}
          >
            <AdminTable headers={['Paper ID', 'Title', 'DOI', 'Journal', 'Year', 'Citations', 'Status', 'Actions']}>
              {filteredPapers.map((paper) => (
                <tr key={paper.id} className="hover:bg-slate-50">
                  <td className="px-5 py-4 font-bold text-slate-700">{paper.id}</td>
                  <td className="max-w-xs px-5 py-4 font-semibold text-slate-900">{paper.title}</td>
                  <td className="px-5 py-4 text-xs text-slate-500">{paper.doi}</td>
                  <td className="px-5 py-4">{paper.journal}</td>
                  <td className="px-5 py-4">{paper.year}</td>
                  <td className="px-5 py-4 font-bold">{paper.citations}</td>
                  <td className="px-5 py-4"><AdminBadge status={paper.status} /></td>
                  <td className="px-5 py-4">
                    <div className="flex gap-2">
                      <button onClick={() => setSelectedPaper(paper)} className="text-xs font-bold text-[#0b6fb8] hover:underline">View</button>
                      <button onClick={() => verifyPaper(paper.id)} className="text-xs font-bold text-emerald-700 hover:underline">Verify</button>
                    </div>
                  </td>
                </tr>
              ))}
            </AdminTable>
          </AdminSectionCard>
        ) : (
          <AdminSectionCard
            title="Concept Registry"
            subtitle="Ontology mapping and classification hierarchy"
            action={<button onClick={() => setShowConceptModal(true)} className="rounded-md bg-[#062b4f] px-4 py-2 text-xs font-bold text-white">+ New Concept</button>}
          >
            <AdminTable headers={['ID', 'Category Name', 'Sub-fields', 'Status']}>
              {categories.map((category) => (
                <tr key={category.id} className="hover:bg-slate-50">
                  <td className="px-5 py-5 font-bold text-slate-500">{category.id}</td>
                  <td className="px-5 py-5">
                    <p className="font-bold text-slate-900">{category.name}</p>
                    <p className="mt-1 max-w-xs text-xs text-slate-500">{category.description}</p>
                  </td>
                  <td className="px-5 py-5"><span className="rounded bg-slate-100 px-3 py-1 text-xs font-bold text-slate-600">{category.fields} Fields</span></td>
                  <td className="px-5 py-5"><AdminBadge status={category.status} /></td>
                </tr>
              ))}
            </AdminTable>
          </AdminSectionCard>
        )}

        <AdminSectionCard
          title="Cleansing Queue"
          action={<span className="rounded bg-red-50 px-2 py-1 text-[10px] font-bold text-red-700">{anomalies.length} ACTIONABLE</span>}
        >
          <div className="space-y-4 p-5">
            {anomalies.length === 0 && <p className="rounded-md bg-emerald-50 p-4 text-sm font-semibold text-emerald-700">No actionable anomalies. Metadata queue is clean.</p>}
            {anomalies.map((anomaly) => (
              <div key={anomaly.id} className={`border-l-4 ${anomalyTone[anomaly.tone]} rounded-r-md bg-slate-50 p-4`}>
                <div className="flex items-center justify-between gap-2">
                  <span className="rounded bg-red-50 px-2 py-1 text-[9px] font-bold text-red-600">{anomaly.label}</span>
                  <AdminBadge status={anomaly.status} />
                </div>
                <h3 className="mt-2 text-sm font-extrabold text-slate-900">{anomaly.title}</h3>
                <p className="mt-1 text-xs text-slate-500">ID: {anomaly.id}</p>
                <div className="mt-3 grid grid-cols-2 gap-2">
                  <button onClick={() => handleAnomalyAction(anomaly)} className="rounded bg-[#062b4f] py-2 text-xs font-bold text-white">{anomaly.action}</button>
                  <button onClick={() => dismissAnomaly(anomaly.id)} className="rounded border border-slate-200 bg-white py-2 text-xs font-bold text-slate-600">Dismiss</button>
                </div>
              </div>
            ))}
            <button onClick={() => setActiveTab('papers')} className="w-full text-center text-sm font-bold text-[#0b6fb8] hover:underline">View All Anomalies</button>
          </div>
        </AdminSectionCard>
      </div>

      <AdminModal
        open={showConceptModal}
        title="Create New Concept"
        subtitle="Add a draft ontology category for later backend mapping."
        onClose={() => setShowConceptModal(false)}
        footer={
          <>
            <button onClick={() => setShowConceptModal(false)} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700">Cancel</button>
            <button onClick={createConcept} className="rounded-md bg-[#062b4f] px-4 py-2 text-xs font-bold text-white">Create Concept</button>
          </>
        }
      >
        <div className="space-y-4">
          <label className="block text-xs font-bold text-slate-700" htmlFor="concept-name">Concept name</label>
          <input id="concept-name" value={conceptName} onChange={(event) => setConceptName(event.target.value)} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]" placeholder="Example: Responsible AI" />
          <label className="block text-xs font-bold text-slate-700" htmlFor="concept-description">Description</label>
          <textarea id="concept-description" value={conceptDescription} onChange={(event) => setConceptDescription(event.target.value)} rows={3} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]" placeholder="Short concept description..." />
        </div>
      </AdminModal>

      <AdminModal
        open={Boolean(selectedPaper)}
        title="Paper Metadata Detail"
        subtitle="Preview metadata before backend update or verification."
        onClose={() => setSelectedPaper(null)}
      >
        {selectedPaper && (
          <div className="space-y-3 text-sm text-slate-700">
            <p><span className="font-bold">Title:</span> {selectedPaper.title}</p>
            <p><span className="font-bold">DOI:</span> {selectedPaper.doi}</p>
            <p><span className="font-bold">Journal:</span> {selectedPaper.journal}</p>
            <p><span className="font-bold">Year:</span> {selectedPaper.year}</p>
            <p><span className="font-bold">Citations:</span> {selectedPaper.citations}</p>
            <p><span className="font-bold">Status:</span> <AdminBadge status={selectedPaper.status} /></p>
          </div>
        )}
      </AdminModal>
    </div>
  );
};

export default AdminRepositoryPage;

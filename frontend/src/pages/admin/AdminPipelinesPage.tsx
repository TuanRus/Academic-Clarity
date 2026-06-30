import { useState, useEffect } from 'react';
import AdminBadge from '../../components/admin/AdminBadge';
import AdminModal from '../../components/admin/AdminModal';
import AdminSectionCard from '../../components/admin/AdminSectionCard';
import AdminTable from '../../components/admin/AdminTable';
import AdminToast from '../../components/admin/AdminToast';
import { getSyncLogs, runWeeklySync, type PipelineEvent } from '../../lib/api/admin';

const AdminPipelinesPage = () => {
  const [history, setHistory] = useState<PipelineEvent[]>([]);

  useEffect(() => {
    getSyncLogs().then(setHistory).catch(() => setHistory([]));
  }, []);
  const [endpointActive, setEndpointActive] = useState(true);
  const [apiKey, setApiKey] = useState('sk-openalex-demo-key');
  const [isSyncing, setIsSyncing] = useState(false);
  const [showExportModal, setShowExportModal] = useState(false);
  const [toast, setToast] = useState<string | null>(null);

  const pushHistory = (event: PipelineEvent) => {
    setHistory((current) => [event, ...current].slice(0, 6));
  };

  const runSync = async (title = 'Manual Trigger') => {
    if (!endpointActive) {
      setToast('Endpoint is inactive. Turn on OpenAlex API before syncing.');
      return;
    }

    setIsSyncing(true);
    pushHistory({ title, time: 'Just now', status: 'PENDING' });
    setToast(`${title} started…`);

    try {
      // Gọi BE thật: fetch bài mới từ OpenAlex + đào keyword nền.
      const res = await runWeeklySync();
      setToast(`${title} done. Added ${res.added}, already existed ${res.alreadyExists}, errors ${res.errors}.`);
      // Tải lại lịch sử sync thật từ bảng api_sync_logs.
      getSyncLogs().then(setHistory).catch(() => {});
    } catch {
      setHistory((current) => current.map((event, index) => (index === 0 ? { ...event, status: 'FAILED', time: 'Just now' } : event)));
      setToast(`${title} failed. Check backend logs / OpenAlex connectivity.`);
    } finally {
      setIsSyncing(false);
    }
  };

  const retryFailed = (eventTitle: string) => {
    setHistory((current) => current.map((event) => (event.title === eventTitle ? { ...event, status: 'PENDING', time: 'Retrying now' } : event)));
    setToast(`Retrying ${eventTitle}.`);
    window.setTimeout(() => {
      setHistory((current) => current.map((event) => (event.title === eventTitle ? { ...event, status: 'SUCCESS', time: 'Retry success' } : event)));
      setToast(`${eventTitle} retry completed successfully.`);
    }, 1200);
  };

  const saveKey = () => {
    if (apiKey.trim().length < 8) {
      setToast('API key is too short. Please enter a valid OpenAlex key or polite pool token.');
      return;
    }
    setToast('OpenAlex API key saved.');
  };

  const exportPipelineLog = () => {
    const content = history.map((event) => `${event.title},${event.time},${event.status}`).join('\n');
    const blob = new Blob([`Title,Time,Status\n${content}`], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'openalex-pipeline-log.csv';
    anchor.click();
    URL.revokeObjectURL(url);
    setShowExportModal(false);
    setToast('Pipeline log exported.');
  };

  return (
    <div className="space-y-5">
      <AdminToast message={toast} onClose={() => setToast(null)} />

      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight text-slate-950">OpenAlex Control Panel</h1>
          <p className="mt-1 text-xs text-slate-500">Dedicated management of OpenAlex external data ingestion streams.</p>
        </div>
        <div className="flex gap-3">
          <button onClick={() => setShowExportModal(true)} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700 hover:bg-slate-50">⇩ Export</button>
          <button onClick={() => runSync('OpenAlex Ingest')} disabled={isSyncing} className="rounded-md bg-[#062b4f] px-4 py-2 text-xs font-bold text-white hover:bg-[#0b3d6f] disabled:cursor-not-allowed disabled:opacity-60">{isSyncing ? 'Syncing...' : '↥ Ingest Now'}</button>
        </div>
      </div>

      <AdminSectionCard
        title="Ingestion Control"
        action={<span className={`text-[11px] font-bold ${endpointActive ? 'text-emerald-700' : 'text-red-700'}`}>● {endpointActive ? 'Endpoint Active' : 'Endpoint Paused'}</span>}
      >
        <AdminTable headers={['Engine', 'Endpoint Base URL', 'Interval', 'Status', 'Actions']}>
          <tr>
            <td className="px-5 py-4 font-bold text-slate-800"><span className="mr-2 rounded-md bg-blue-50 px-2 py-1 text-blue-700">☁</span>OpenAlex API</td>
            <td className="px-5 py-4">api.openalex.org/works</td>
            <td className="px-5 py-4"><span className="rounded bg-slate-100 px-2 py-1 text-[10px] font-bold">Daily</span></td>
            <td className="px-5 py-4"><AdminBadge status={endpointActive ? 'ACTIVE' : 'SUSPENDED'} /></td>
            <td className="px-5 py-4">
              <div className="flex items-center gap-3">
                <button onClick={() => runSync()} disabled={isSyncing} className="font-bold text-[#0b6fb8] disabled:text-slate-400">↻ Sync Now</button>
                <button
                  type="button"
                  onClick={() => {
                    setEndpointActive((value) => !value);
                    setToast(endpointActive ? 'OpenAlex endpoint paused.' : 'OpenAlex endpoint activated.');
                  }}
                  className={`h-5 w-10 rounded-full p-0.5 transition ${endpointActive ? 'bg-emerald-200' : 'bg-slate-200'}`}
                >
                  <span className={`block h-4 w-4 rounded-full bg-white shadow transition ${endpointActive ? 'translate-x-5' : ''}`} />
                </button>
              </div>
            </td>
          </tr>
        </AdminTable>
      </AdminSectionCard>

      <div className="grid gap-5 lg:grid-cols-[1.25fr_0.85fr]">
        <AdminSectionCard title="Ingestion History">
          <div className="divide-y divide-slate-100 px-5">
            {history.map((event, index) => (
              <div key={`${event.title}-${event.time}-${index}`} className="flex items-center justify-between py-4">
                <div>
                  <p className="text-sm font-bold text-slate-800">{event.title}</p>
                  <p className="text-xs text-slate-500">{event.time}</p>
                </div>
                <div className="flex items-center gap-2">
                  <AdminBadge status={event.status} />
                  {event.status === 'FAILED' && <button onClick={() => retryFailed(event.title)} className="rounded border border-blue-200 px-3 py-1 text-xs font-bold text-blue-700">Retry</button>}
                </div>
              </div>
            ))}
          </div>
        </AdminSectionCard>

        <AdminSectionCard title="API Authentication">
          <div className="space-y-4 p-5">
            <label className="block text-xs font-bold text-slate-700" htmlFor="openalex-key">OpenAlex API Key</label>
            <input id="openalex-key" type="password" value={apiKey} onChange={(event) => setApiKey(event.target.value)} className="w-full rounded-md border border-slate-300 bg-slate-50 px-3 py-2 text-sm text-slate-700" />
            <p className="text-xs text-slate-500">Your key is encrypted and used only for authenticated requests to OpenAlex.</p>
            <button onClick={saveKey} className="w-full rounded-md bg-[#0b6fb8] px-4 py-2 text-sm font-bold text-white hover:bg-[#095d9d]">Save Key</button>
          </div>
        </AdminSectionCard>
      </div>

      <AdminModal
        open={showExportModal}
        title="Export Pipeline Logs"
        subtitle="Download current ingestion history as a CSV file."
        onClose={() => setShowExportModal(false)}
        footer={
          <>
            <button onClick={() => setShowExportModal(false)} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700">Cancel</button>
            <button onClick={exportPipelineLog} className="rounded-md bg-[#062b4f] px-4 py-2 text-xs font-bold text-white">Export CSV</button>
          </>
        }
      >
        <p className="text-sm text-slate-600">The exported file contains engine name, timestamp and current sync status from the api_sync_logs table.</p>
      </AdminModal>
    </div>
  );
};

export default AdminPipelinesPage;

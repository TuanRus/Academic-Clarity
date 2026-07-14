import { useEffect, useRef, useState } from 'react';
import AdminBadge from '../../components/admin/AdminBadge';
import AdminModal from '../../components/admin/AdminModal';
import AdminSectionCard from '../../components/admin/AdminSectionCard';
import AdminTable from '../../components/admin/AdminTable';
import AdminToast from '../../components/admin/AdminToast';
import {
  getSyncLogs, startLiveSync, getSyncProgress, getSyncedPapers,
  type PipelineEvent, type SyncedPaper, type SyncProgress,
} from '../../lib/api/admin';

// Màu chữ trạng thái realtime của Live Monitor.
const liveStatusColor = (s: string) =>
  s === 'Success' ? 'font-bold text-emerald-700'
  : s === 'Error' ? 'font-bold text-red-600'
  : s === 'Exists' ? 'text-slate-500'
  : 'text-amber-600';

type ApiSource = {
  id: number;
  engine: string;
  endpoint: string;
  interval: string;
  status: 'ACTIVE' | 'SUSPENDED';
};

const AdminPipelinesPage = () => {
  const [sources, setSources] = useState<ApiSource[]>([
    {
      id: 1,
      engine: 'OpenAlex API',
      endpoint: 'api.openalex.org/works',
      interval: 'Daily',
      status: 'ACTIVE',
    },
  ]);

  const [history, setHistory] = useState<PipelineEvent[]>([]);
  useEffect(() => { getSyncLogs().then(setHistory).catch(() => setHistory([])); }, []);
  const [apiKey, setApiKey] = useState('sk-openalex-demo-key');
  const [isSyncing, setIsSyncing] = useState(false);
  const [showAddSourceModal, setShowAddSourceModal] = useState(false);
  const [toast, setToast] = useState<string | null>(null);

  const [newSource, setNewSource] = useState({
    engine: '',
    endpoint: '',
    interval: 'Daily',
  });

  const hasActiveEndpoint = sources.some((source) => source.status === 'ACTIVE');

  // ----- Detail modal: bài đã sync của 1 lần -----
  const [detailOpen, setDetailOpen] = useState(false);
  const [detailTitle, setDetailTitle] = useState('');
  const [detailPapers, setDetailPapers] = useState<SyncedPaper[]>([]);
  const [detailLoading, setDetailLoading] = useState(false);

  const openDetail = async (event: PipelineEvent) => {
    if (event.id == null) return;
    setDetailTitle(event.title);
    setDetailPapers([]);
    setDetailOpen(true);
    setDetailLoading(true);
    try { setDetailPapers(await getSyncedPapers(event.id)); }
    catch { setDetailPapers([]); }
    finally { setDetailLoading(false); }
  };

  // ----- Live Sync Monitor: chạy nền + poll realtime -----
  const [liveOpen, setLiveOpen] = useState(false);
  const [live, setLive] = useState<SyncProgress | null>(null);
  const pollRef = useRef<number | null>(null);

  const stopPolling = () => { if (pollRef.current) { window.clearInterval(pollRef.current); pollRef.current = null; } };
  const startPolling = () => {
    stopPolling();
    const tick = async () => {
      try {
        const p = await getSyncProgress();
        setLive(p);
        if (!p.isRunning) { stopPolling(); setIsSyncing(false); getSyncLogs().then(setHistory).catch(() => {}); }
      } catch { /* bỏ qua 1 nhịp lỗi */ }
    };
    tick();
    pollRef.current = window.setInterval(tick, 1500);
  };
  const openLive = () => { setLiveOpen(true); startPolling(); };
  const closeLive = () => { setLiveOpen(false); stopPolling(); };
  useEffect(() => () => stopPolling(), []);

  const runSync = async (title = 'Manual Trigger') => {
    if (!hasActiveEndpoint) {
      setToast('No active API source. Please activate at least one endpoint before syncing.');
      return;
    }
    setIsSyncing(true);
    try {
      await startLiveSync(2); // chạy nền → theo dõi realtime
      setToast(`${title} started — watching live…`);
      getSyncLogs().then(setHistory).catch(() => {});
      openLive();
    } catch {
      setIsSyncing(false);
      setToast(`${title} could not start (a sync may already be running).`);
    }
  };

  const runSourceSync = (source: ApiSource) => {
    if (source.status !== 'ACTIVE') {
      setToast(`${source.engine} is suspended. Please activate it before syncing.`);
      return;
    }

    runSync(`${source.engine} Sync`);
  };

  const retryFailed = (eventTitle: string) => {
    setHistory((current) =>
      current.map((event) =>
        event.title === eventTitle
          ? { ...event, status: 'PENDING', time: 'Retrying now' }
          : event,
      ),
    );

    setToast(`Retrying ${eventTitle}.`);

    window.setTimeout(() => {
      setHistory((current) =>
        current.map((event) =>
          event.title === eventTitle
            ? { ...event, status: 'SUCCESS', time: 'Retry success' }
            : event,
        ),
      );

      setToast(`${eventTitle} retry completed successfully.`);
    }, 1200);
  };

  const toggleSourceStatus = (sourceId: number) => {
    setSources((current) =>
      current.map((source) =>
        source.id === sourceId
          ? {
              ...source,
              status: source.status === 'ACTIVE' ? 'SUSPENDED' : 'ACTIVE',
            }
          : source,
      ),
    );

    const selectedSource = sources.find((source) => source.id === sourceId);

    if (selectedSource) {
      setToast(
        selectedSource.status === 'ACTIVE'
          ? `${selectedSource.engine} endpoint paused.`
          : `${selectedSource.engine} endpoint activated.`,
      );
    }
  };

  const addSource = () => {
    if (!newSource.engine.trim()) {
      setToast('Please enter API engine name.');
      return;
    }

    if (!newSource.endpoint.trim()) {
      setToast('Please enter endpoint base URL.');
      return;
    }

    const source: ApiSource = {
      id: Date.now(),
      engine: newSource.engine.trim(),
      endpoint: newSource.endpoint.trim(),
      interval: newSource.interval,
      status: 'ACTIVE',
    };

    setSources((current) => [...current, source]);
    setNewSource({
      engine: '',
      endpoint: '',
      interval: 'Daily',
    });

    setShowAddSourceModal(false);
    setToast(`${source.engine} source added successfully.`);
  };

  const removeSource = (sourceId: number) => {
    const selectedSource = sources.find((source) => source.id === sourceId);

    setSources((current) => current.filter((source) => source.id !== sourceId));

    if (selectedSource) {
      setToast(`${selectedSource.engine} source removed.`);
    }
  };

  const saveKey = () => {
    if (apiKey.trim().length < 8) {
      setToast('API key is too short. Please enter a valid OpenAlex key or polite pool token.');
      return;
    }

    setToast('API key saved locally for demo.');
  };

  return (
    <div className="space-y-5">
      <AdminToast message={toast} onClose={() => setToast(null)} />

      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight text-slate-950">
            OpenAlex Control Panel
          </h1>
          <p className="mt-1 text-xs text-slate-500">
            Dedicated management of external data ingestion streams.
          </p>
        </div>

        <div className="flex gap-3">
          <button
            onClick={() => setShowAddSourceModal(true)}
            className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700 hover:bg-slate-50"
          >
            + Add API Source
          </button>

          <button
            onClick={() => runSync('Manual Ingest')}
            disabled={isSyncing}
            className="rounded-md bg-[#4338ca] px-4 py-2 text-xs font-bold text-white hover:bg-[#3730a3] disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSyncing ? 'Syncing...' : '↥ Ingest Now'}
          </button>
        </div>
      </div>

      <AdminSectionCard
        title="Ingestion Control"
        action={
          <span
            className={`text-[11px] font-bold ${
              hasActiveEndpoint ? 'text-emerald-700' : 'text-red-700'
            }`}
          >
            ● {hasActiveEndpoint ? 'Endpoint Active' : 'All Endpoints Paused'}
          </span>
        }
      >
        <AdminTable
          headers={['Engine', 'Endpoint Base URL', 'Interval', 'Status', 'Actions']}
        >
          {sources.map((source) => (
            <tr key={source.id}>
              <td className="px-5 py-4 font-bold text-slate-800">
                <span className="mr-2 rounded-md bg-blue-50 px-2 py-1 text-blue-700">
                  ☁
                </span>
                {source.engine}
              </td>

              <td className="px-5 py-4">{source.endpoint}</td>

              <td className="px-5 py-4">
                <span className="rounded bg-slate-100 px-2 py-1 text-[10px] font-bold">
                  {source.interval}
                </span>
              </td>

              <td className="px-5 py-4">
                <AdminBadge status={source.status} />
              </td>

              <td className="px-5 py-4">
                <div className="flex items-center gap-3">
                  <button
                    onClick={() => runSourceSync(source)}
                    disabled={isSyncing}
                    className="font-bold text-[#0b6fb8] disabled:text-slate-400"
                  >
                    ↻ Sync Now
                  </button>

                  <button
                    type="button"
                    onClick={() => toggleSourceStatus(source.id)}
                    className={`h-5 w-10 rounded-full p-0.5 transition ${
                      source.status === 'ACTIVE' ? 'bg-emerald-200' : 'bg-slate-200'
                    }`}
                  >
                    <span
                      className={`block h-4 w-4 rounded-full bg-white shadow transition ${
                        source.status === 'ACTIVE' ? 'translate-x-5' : ''
                      }`}
                    />
                  </button>

                  {sources.length > 1 && (
                    <button
                      onClick={() => removeSource(source.id)}
                      className="text-xs font-bold text-red-600 hover:underline"
                    >
                      Remove
                    </button>
                  )}
                </div>
              </td>
            </tr>
          ))}
        </AdminTable>
      </AdminSectionCard>

      <div className="grid gap-5 lg:grid-cols-[1.25fr_0.85fr]">
        <AdminSectionCard title="Ingestion History">
          <div className="divide-y divide-slate-100 px-5">
            {history.map((event, index) => (
              <div
                key={`${event.title}-${event.time}-${index}`}
                className="flex items-center justify-between py-4"
              >
                <div>
                  <p className="text-sm font-bold text-slate-800">{event.title}</p>
                  <p className="text-xs text-slate-500">{event.time}</p>
                </div>

                <div className="flex items-center gap-2">
                  <AdminBadge status={event.status} />

                  {(event.status === 'RUNNING' || event.status === 'PENDING') && (
                    <button
                      onClick={openLive}
                      className="rounded border border-sky-300 bg-sky-50 px-3 py-1 text-xs font-bold text-sky-700 hover:bg-sky-100"
                    >
                      ● Live
                    </button>
                  )}

                  {event.id != null && event.status !== 'RUNNING' && (
                    <button
                      onClick={() => openDetail(event)}
                      className="rounded border border-slate-300 px-3 py-1 text-xs font-bold text-slate-700 hover:bg-slate-50"
                    >
                      Detail
                    </button>
                  )}

                  {event.status === 'FAILED' && (
                    <button
                      onClick={() => retryFailed(event.title)}
                      className="rounded border border-blue-200 px-3 py-1 text-xs font-bold text-blue-700"
                    >
                      Retry
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        </AdminSectionCard>

        <AdminSectionCard title="API Authentication">
          <div className="space-y-4 p-5">
            <label
              className="block text-xs font-bold text-slate-700"
              htmlFor="openalex-key"
            >
              API Key / Token
            </label>

            <input
              id="openalex-key"
              type="password"
              value={apiKey}
              onChange={(event) => setApiKey(event.target.value)}
              className="w-full rounded-md border border-slate-300 bg-slate-50 px-3 py-2 text-sm text-slate-700"
            />

            <p className="text-xs text-slate-500">
              Your key is encrypted and used only for authenticated requests to external
              academic APIs.
            </p>

            <button
              onClick={saveKey}
              className="w-full rounded-md bg-[#4338ca] px-4 py-2 text-sm font-bold text-white hover:bg-[#3730a3]"
            >
              Save Key
            </button>
          </div>
        </AdminSectionCard>
      </div>

      <AdminModal
        open={showAddSourceModal}
        title="Add API Source"
        subtitle="Register a new external academic API endpoint for ingestion."
        onClose={() => setShowAddSourceModal(false)}
        footer={
          <>
            <button
              onClick={() => setShowAddSourceModal(false)}
              className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700"
            >
              Cancel
            </button>

            <button
              onClick={addSource}
              className="rounded-md bg-[#4338ca] px-4 py-2 text-xs font-bold text-white"
            >
              Add Source
            </button>
          </>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="mb-1 block text-xs font-bold text-slate-700">
              Engine Name
            </label>
            <input
              value={newSource.engine}
              onChange={(event) =>
                setNewSource((current) => ({
                  ...current,
                  engine: event.target.value,
                }))
              }
              placeholder="Example: Semantic Scholar API"
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]"
            />
          </div>

          <div>
            <label className="mb-1 block text-xs font-bold text-slate-700">
              Endpoint Base URL
            </label>
            <input
              value={newSource.endpoint}
              onChange={(event) =>
                setNewSource((current) => ({
                  ...current,
                  endpoint: event.target.value,
                }))
              }
              placeholder="Example: api.semanticscholar.org/graph/v1/paper/search"
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]"
            />
          </div>

          <div>
            <label className="mb-1 block text-xs font-bold text-slate-700">
              Sync Interval
            </label>
            <select
              value={newSource.interval}
              onChange={(event) =>
                setNewSource((current) => ({
                  ...current,
                  interval: event.target.value,
                }))
              }
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#4338ca]"
            >
              <option value="Every 6h">Every 6h</option>
              <option value="Daily">Daily</option>
              <option value="Weekly">Weekly</option>
            </select>
          </div>
        </div>
      </AdminModal>

      <AdminModal
        open={detailOpen}
        title={`Bài báo đã sync — ${detailTitle}`}
        subtitle="Các bài được thêm trong khung thời gian của lần sync này."
        onClose={() => setDetailOpen(false)}
        footer={<button onClick={() => setDetailOpen(false)} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700">Close</button>}
      >
        {detailLoading ? (
          <p className="text-sm text-slate-500">Đang tải…</p>
        ) : detailPapers.length === 0 ? (
          <p className="text-sm text-slate-500">Không có bài nào được thêm trong lần sync này.</p>
        ) : (
          <div className="max-h-[60vh] overflow-auto">
            <p className="mb-2 text-xs font-semibold text-slate-500">{detailPapers.length} bài</p>
            <ul className="space-y-2">
              {detailPapers.map((p) => (
                <li key={p.paperId} className="rounded border border-slate-100 p-2">
                  <p className="text-sm font-medium text-slate-800">{p.title}</p>
                  <p className="text-xs text-slate-500">
                    {p.publicationYear ?? '—'} · {p.openAlexId ?? p.paperId}
                    {p.sourceUrl && (<> · <a href={p.sourceUrl} target="_blank" rel="noreferrer" className="text-[#0b6fb8] underline">OpenAlex</a></>)}
                  </p>
                </li>
              ))}
            </ul>
          </div>
        )}
      </AdminModal>

      <AdminModal
        open={liveOpen}
        title="Live Sync Monitor"
        subtitle="Realtime — only NEWLY synced papers (matches Detail): time · paper · status."
        onClose={closeLive}
        footer={<button onClick={closeLive} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700">Close</button>}
      >
        {!live ? (
          <p className="text-sm text-slate-500">Connecting…</p>
        ) : (
          <div>
            <div className="mb-3 flex flex-wrap items-center gap-4 text-xs font-semibold">
              <span className={live.isRunning ? 'text-sky-700' : 'text-emerald-700'}>{live.isRunning ? '● RUNNING' : '✓ FINISHED'}</span>
              <span className="text-slate-600">Total: {live.total}</span>
              <span className="text-emerald-700">Added: {live.added}</span>
              <span className="text-slate-500">Exists: {live.exists}</span>
              <span className="text-red-600">Errors: {live.errors}</span>
            </div>
            <div className="max-h-[55vh] overflow-auto rounded border border-slate-100">
              <table className="w-full text-left text-xs">
                <thead className="sticky top-0 bg-slate-50 text-slate-500">
                  <tr><th className="px-2 py-1">Time</th><th className="px-2 py-1">Paper</th><th className="px-2 py-1">Status</th></tr>
                </thead>
                <tbody>
                  {[...live.entries].reverse().map((e, i) => (
                    <tr key={i} className="border-t border-slate-100">
                      <td className="whitespace-nowrap px-2 py-1 text-slate-500">{new Date(e.time).toLocaleTimeString()}</td>
                      <td className="px-2 py-1 text-slate-800">{e.title}</td>
                      <td className="px-2 py-1"><span className={liveStatusColor(e.status)}>{e.status}</span></td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {live.entries.length === 0 && (
                <p className="p-2 text-sm text-slate-500">{live.isRunning ? 'Waiting for the first newly synced paper…' : 'No new papers were added in this run.'}</p>
              )}
            </div>
          </div>
        )}
      </AdminModal>

    </div>
  );
};

export default AdminPipelinesPage;
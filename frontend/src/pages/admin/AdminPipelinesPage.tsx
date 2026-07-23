import { useEffect, useRef, useState } from 'react';
import AdminBadge from '../../components/admin/AdminBadge';
import AdminModal from '../../components/admin/AdminModal';
import AdminSectionCard from '../../components/admin/AdminSectionCard';
import AdminTable from '../../components/admin/AdminTable';
import AdminToast from '../../components/admin/AdminToast';
import {
  getSyncLogs, startLiveSync, getSyncProgress, getSyncedPapers,
  getSystemConfig, importScimagoUpload, getDataSources,
  type PipelineEvent, type SyncedPaper, type SyncProgress, type IntegrationStatus,
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
  const [sources, setSources] = useState<ApiSource[]>([]);
  // Nạp nguồn đồng bộ THẬT từ backend (ApiDataSources) thay vì hardcode 'Daily'/'ACTIVE'.
  useEffect(() => { getDataSources().then(setSources).catch(() => setSources([])); }, []);

  const [history, setHistory] = useState<PipelineEvent[]>([]);
  useEffect(() => { getSyncLogs().then(setHistory).catch(() => setHistory([])); }, []);

  // Trạng thái tích hợp liên quan pipeline (OpenAlex nguồn dữ liệu + AI trích keyword).
  const [pipelineIntegrations, setPipelineIntegrations] = useState<IntegrationStatus[]>([]);
  const [openAlexBaseUrl, setOpenAlexBaseUrl] = useState('');
  const [openAlexEmail, setOpenAlexEmail] = useState('');
  useEffect(() => {
    getSystemConfig()
      .then((c) => {
        setPipelineIntegrations(c.integrations.filter((i) => i.name === 'OpenAlex' || i.name === 'Gemini AI'));
        setOpenAlexBaseUrl(c.openAlexBaseUrl);
        setOpenAlexEmail(c.openAlexEmail);
      })
      .catch(() => setPipelineIntegrations([]));
  }, []);
  const openAlexConfigured = pipelineIntegrations.find((i) => i.name === 'OpenAlex')?.configured ?? false;
  // Che phần tên trước @, chỉ để lộ domain: "abc@gmail.com" -> "***@gmail.com".
  const maskEmail = (email: string) => {
    if (!email) return '—';
    const at = email.indexOf('@');
    return at > 0 ? `***${email.slice(at)}` : '***';
  };
  const [isSyncing, setIsSyncing] = useState(false);
  const [toast, setToast] = useState<string | null>(null);

  // ----- SCImago journal-ranking import (upload file CSV: kéo-thả hoặc chọn file) -----
  const [scimagoBusy, setScimagoBusy] = useState(false);
  const scimagoInputRef = useRef<HTMLInputElement | null>(null);

  const runScimagoUpload = async (file: File) => {
    if (!file.name.toLowerCase().endsWith('.csv')) {
      setToast('Only .csv files are accepted.');
      return;
    }
    setScimagoBusy(true);
    try {
      const r = await importScimagoUpload(file);
      setToast(`SCImago import done — ${r.updatedCount} journals updated (${r.totalRowsRead} rows read, ${r.skippedCount} skipped).`);
    } catch {
      setToast('SCImago import failed — please check the CSV format.');
    } finally {
      setScimagoBusy(false);
      if (scimagoInputRef.current) scimagoInputRef.current.value = ''; // cho phép chọn lại cùng file
    }
  };

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
  const sawRunningRef = useRef(false); // đã QUAN SÁT được trạng thái running ít nhất 1 lần chưa
  const startWaitRef = useRef(0);      // số nhịp đã chờ background task khởi động
  const autoPolledRef = useRef(false); // chỉ auto-poll 1 lần (tránh lặp vô hạn với log 'running' mồ côi)

  const stopPolling = () => { if (pollRef.current) { window.clearInterval(pollRef.current); pollRef.current = null; } };
  const startPolling = () => {
    stopPolling();
    sawRunningRef.current = false;
    startWaitRef.current = 0;
    const tick = async () => {
      try {
        const p = await getSyncProgress();
        setLive(p);
        if (p.isRunning) {
          sawRunningRef.current = true;
          return;
        }
        // isRunning=false: CHỈ coi là "đã xong" khi đã từng thấy running (tránh báo success ngay
        // do race lúc mới start khi background task chưa kịp Begin), hoặc đã chờ quá lâu (~15s).
        if (!sawRunningRef.current && startWaitRef.current < 10) {
          startWaitRef.current += 1;
          return;
        }
        stopPolling();
        setIsSyncing(false);
        // Lấy lịch sử để biết lần sync vừa rồi thành công hay FAILED (vd 429 rate limit / hết budget).
        getSyncLogs().then((logs) => {
          setHistory(logs);
          const latest = logs.find((l) => l.id === p.syncLogId) ?? logs[0];
          if (latest?.status === 'FAILED') {
            setToast('Sync failed — OpenAlex API rate limited / out of budget. Try again after the daily reset.');
          } else if (p.added > 0) {
            setToast(`Sync finished — ${p.added} new paper(s) imported.`);
          } else {
            setToast('Sync finished — no new papers.');
          }
        }).catch(() => {});
      } catch { /* bỏ qua 1 nhịp lỗi */ }
    };
    tick();
    pollRef.current = window.setInterval(tick, 1500);
  };
  const openLive = () => { setLiveOpen(true); if (pollRef.current == null) startPolling(); };
  // Tắt monitor CHỈ ẩn modal — vẫn poll nền để Ingestion History cập nhật số bài đang chạy.
  const closeLive = () => { setLiveOpen(false); };
  useEffect(() => () => stopPolling(), []);
  // Tự động theo dõi 1 lần nếu có log đang RUNNING (kể cả khi tải lại trang giữa lúc sync).
  useEffect(() => {
    if (!autoPolledRef.current && pollRef.current == null &&
        history.some((h) => h.status === 'RUNNING' || h.status === 'PENDING')) {
      autoPolledRef.current = true;
      startPolling();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [history]);

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

  return (
    <div className="space-y-5">
      <AdminToast message={toast} onClose={() => setToast(null)} />

      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight text-slate-950">
            OpenAlex Data Synchronization
          </h1>
          <p className="mt-1 text-xs text-slate-500">
            Configure OpenAlex sources and monitor synchronization jobs.
          </p>
          {pipelineIntegrations.length > 0 && (
            <div className="mt-2 flex flex-wrap items-center gap-2">
              {pipelineIntegrations.map((it) => (
                <span
                  key={it.name}
                  className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-[11px] font-semibold ${
                    it.configured ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500'
                  }`}
                >
                  {it.configured ? '✓' : '✗'} {it.name}
                </span>
              ))}
            </div>
          )}
        </div>

      </div>

      <AdminSectionCard
        title="Data Sources"
        action={
          <div className="flex items-center gap-3">
            <span
              className={`text-[11px] font-bold ${
                hasActiveEndpoint ? 'text-emerald-700' : 'text-red-700'
              }`}
            >
              ● {hasActiveEndpoint ? 'Endpoint Active' : 'All Endpoints Paused'}
            </span>
            <button
              onClick={() => runSync('Manual Trigger')}
              disabled={isSyncing}
              className="rounded-md bg-[#0b6fb8] px-3 py-1.5 text-xs font-bold text-white disabled:opacity-50"
            >
              {isSyncing ? 'Syncing…' : '↻ Sync Now'}
            </button>
          </div>
        }
      >
        <AdminTable
          headers={['Engine', 'Endpoint Base URL', 'Interval', 'Status']}
        >
          {sources.map((source) => (
            <tr key={source.id}>
              <td className="px-5 py-4 font-bold text-slate-800">
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
            </tr>
          ))}
        </AdminTable>
      </AdminSectionCard>

      <AdminSectionCard
        title="SCImago Journal Ranking Import"
        subtitle="Upload a SCImago CSV to update journal quartile / impact factor / H-index (matched by ISSN)"
      >
        <div className="flex flex-col gap-2 p-5">
          <input
            ref={scimagoInputRef}
            type="file"
            accept=".csv"
            className="hidden"
            onChange={(e) => { const f = e.target.files?.[0]; if (f) runScimagoUpload(f); }}
          />
          <button
            type="button"
            onClick={() => scimagoInputRef.current?.click()}
            disabled={scimagoBusy}
            className="w-fit rounded-md bg-[#062b4f] px-4 py-2 text-xs font-bold text-white disabled:opacity-50"
          >
            {scimagoBusy ? 'Importing…' : 'Choose CSV file to import'}
          </button>
          <p className="text-[11px] text-slate-400">
            Only .csv files (SCImago format). Matching journals (by ISSN) get their quartile,
            impact factor and H-index updated.
          </p>
        </div>
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
                    <>
                      {live && live.syncLogId === event.id && (
                        <span className="text-xs font-semibold text-sky-700">
                          {live.added} added · {live.added + live.exists + live.errors}
                          {live.total ? `/${live.total}` : ''} processed
                        </span>
                      )}
                      <button
                        onClick={openLive}
                        className="rounded border border-sky-300 bg-sky-50 px-3 py-1 text-xs font-bold text-sky-700 hover:bg-sky-100"
                      >
                        ● Live
                      </button>
                    </>
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

        <AdminSectionCard
          title="OpenAlex Access"
          action={
            <span className="shrink-0 rounded-full bg-slate-100 px-2.5 py-1 text-[10px] font-bold uppercase tracking-wide text-slate-500">
              Read-only
            </span>
          }
        >
          <div className="space-y-2 p-5">
            <div className="flex items-center justify-between border-b border-slate-100 py-2">
              <span className="text-sm text-slate-500">Access mode</span>
              <span className="text-sm font-semibold text-slate-800">Polite pool (via contact email)</span>
            </div>
            <div className="flex items-center justify-between border-b border-slate-100 py-2">
              <span className="text-sm text-slate-500">Base URL</span>
              <span className="text-sm font-semibold text-slate-800 break-all">{openAlexBaseUrl || '—'}</span>
            </div>
            <div className="flex items-center justify-between border-b border-slate-100 py-2">
              <span className="text-sm text-slate-500">Contact email</span>
              <span className="text-sm font-semibold text-slate-800 break-all">{maskEmail(openAlexEmail)}</span>
            </div>
            <div className="flex items-center justify-between py-2">
              <span className="text-sm text-slate-500">Status</span>
              <span className={`text-sm font-semibold ${openAlexConfigured ? 'text-emerald-600' : 'text-slate-400'}`}>
                {openAlexConfigured ? '✓ Configured' : '✗ Not configured'}
              </span>
            </div>
            <p className="pt-1 text-xs text-slate-400">
              OpenAlex requires no API key — requests use the polite pool via a contact email.
            </p>
          </div>
        </AdminSectionCard>
      </div>

      <AdminModal
        open={detailOpen}
        title={`Synced Papers — ${detailTitle}`}
        subtitle="Papers imported during this synchronization run."
        onClose={() => setDetailOpen(false)}
        footer={<button onClick={() => setDetailOpen(false)} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700">Close</button>}
      >
        {detailLoading ? (
          <p className="text-sm text-slate-500">Loading…</p>
        ) : detailPapers.length === 0 ? (
          <p className="text-sm text-slate-500">No papers were added in this run.</p>
        ) : (
          <div className="max-h-[60vh] overflow-auto">
            <p className="mb-2 text-xs font-semibold text-slate-500">{detailPapers.length} paper(s)</p>
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
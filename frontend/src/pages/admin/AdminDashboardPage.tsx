import { useMemo, useState, useEffect } from 'react';
import AdminBadge from '../../components/admin/AdminBadge';
import AdminMetricCard from '../../components/admin/AdminMetricCard';
import AdminModal from '../../components/admin/AdminModal';
import AdminSectionCard from '../../components/admin/AdminSectionCard';
import AdminTable from '../../components/admin/AdminTable';
import AdminToast from '../../components/admin/AdminToast';
import { getDashboardStats, type DashboardStats, type ExportRequest } from '../../lib/api/admin';

const AdminDashboardPage = () => {
  // Export requests là tính năng FE, chưa có endpoint BE → để trống.
  const [requests, setRequests] = useState<ExportRequest[]>([]);
  const [stats, setStats] = useState<DashboardStats | null>(null);

  useEffect(() => {
    getDashboardStats().then(setStats).catch(() => setStats(null));
  }, []);
  const [showAll, setShowAll] = useState(false);
  const [selectedRequest, setSelectedRequest] = useState<ExportRequest | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  const visibleRequests = showAll ? requests : requests.slice(0, 4);
  const readyCount = useMemo(() => requests.filter((request) => request.status === 'READY').length, [requests]);

  const createExportRequest = (type: 'CSV' | 'PDF') => {
    const now = new Date();
    const nextRequest: ExportRequest = {
      id: `#EXP-${82920 + requests.length}`,
      user: 'admin_root',
      type,
      timestamp: now.toISOString().slice(0, 16).replace('T', ' '),
      status: 'GENERATING',
    };

    setRequests((current) => [nextRequest, ...current]);
    setToast(`${type} export request has been queued.`);

    window.setTimeout(() => {
      setRequests((current) => current.map((request) => (request.id === nextRequest.id ? { ...request, status: 'READY' } : request)));
      setToast(`${type} export is ready to download.`);
    }, 1200);
  };

  const downloadReport = (request: ExportRequest) => {
    if (request.status !== 'READY') {
      setToast('This report is not ready yet. Please wait until the status becomes READY.');
      return;
    }

    const content = `Report ID,User,Type,Timestamp,Status\n${request.id},${request.user},${request.type},${request.timestamp},${request.status}`;
    const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${request.id.replace('#', '')}.${request.type.toLowerCase() === 'pdf' ? 'txt' : 'csv'}`;
    anchor.click();
    URL.revokeObjectURL(url);
    setToast(`Downloaded ${request.id}.`);
  };

  return (
    <div className="space-y-5">
      <AdminToast message={toast} onClose={() => setToast(null)} />

      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight text-slate-950">System Workspace: Dashboard Overview</h1>
          <p className="mt-1 text-xs text-slate-500">Real-time ingestion metrics and governance ledger status.</p>
        </div>
        <div className="flex gap-3">
          <button onClick={() => createExportRequest('CSV')} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700 hover:bg-slate-50">Export CSV</button>
          <button onClick={() => createExportRequest('PDF')} className="rounded-md bg-[#062b4f] px-4 py-2 text-xs font-bold text-white hover:bg-[#0b3d6f]">Export PDF</button>
        </div>
      </div>

      <div className="grid gap-5 lg:grid-cols-3">
        <AdminMetricCard label="Total Articles Fetched" value={(stats?.totalPapers ?? 0).toLocaleString('vi-VN')} helper="Records from OpenAlex crawl" icon="☁" accent="blue" />
        <AdminMetricCard label="Total Authors" value={(stats?.totalAuthors ?? 0).toLocaleString('vi-VN')} helper="Authors in repository" icon="▣" accent="blue" />
        <AdminMetricCard label="Ready Reports" value={String(readyCount)} helper="Reports available for download" icon="✓" accent="green" />
      </div>

      <AdminSectionCard
        title="Recent Export Requests"
        action={<button onClick={() => setShowAll((value) => !value)} className="text-xs font-bold text-[#0b6fb8] hover:underline">{showAll ? 'Show Less' : 'View All'}</button>}
      >
        <AdminTable headers={['Report ID', 'User', 'Type', 'Timestamp', 'Status', 'Actions']}>
          {visibleRequests.map((request) => (
            <tr key={request.id} className="hover:bg-slate-50">
              <td className="px-5 py-4 font-bold text-slate-700">{request.id}</td>
              <td className="px-5 py-4">{request.user}</td>
              <td className="px-5 py-4 font-semibold">{request.type}</td>
              <td className="px-5 py-4">{request.timestamp}</td>
              <td className="px-5 py-4"><AdminBadge status={request.status} /></td>
              <td className="px-5 py-4">
                <div className="flex gap-2">
                  <button onClick={() => setSelectedRequest(request)} className="text-xs font-bold text-[#0b6fb8] hover:underline">Detail</button>
                  <button onClick={() => downloadReport(request)} className="text-xs font-bold text-slate-700 hover:text-[#062b4f]">Download</button>
                </div>
              </td>
            </tr>
          ))}
        </AdminTable>
      </AdminSectionCard>

      <AdminModal
        open={Boolean(selectedRequest)}
        title="Export Request Detail"
        subtitle="Report lifecycle: Requested → Generating → Ready / Failed."
        onClose={() => setSelectedRequest(null)}
        footer={
          <>
            <button onClick={() => setSelectedRequest(null)} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700">Close</button>
            {selectedRequest && <button onClick={() => downloadReport(selectedRequest)} className="rounded-md bg-[#062b4f] px-4 py-2 text-xs font-bold text-white">Download</button>}
          </>
        }
      >
        {selectedRequest && (
          <div className="grid gap-3 text-sm text-slate-700 sm:grid-cols-2">
            <p><span className="font-bold">Report ID:</span> {selectedRequest.id}</p>
            <p><span className="font-bold">User:</span> {selectedRequest.user}</p>
            <p><span className="font-bold">Type:</span> {selectedRequest.type}</p>
            <p><span className="font-bold">Timestamp:</span> {selectedRequest.timestamp}</p>
            <p className="sm:col-span-2"><span className="font-bold">Status:</span> <AdminBadge status={selectedRequest.status} /></p>
          </div>
        )}
      </AdminModal>
    </div>
  );
};

export default AdminDashboardPage;

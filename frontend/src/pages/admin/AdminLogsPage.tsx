import { useState } from 'react';
import AdminBadge from '../../components/admin/AdminBadge';
import AdminMetricCard from '../../components/admin/AdminMetricCard';
import AdminSectionCard from '../../components/admin/AdminSectionCard';
import AdminTable from '../../components/admin/AdminTable';

type LogStatus = 'SUCCESS' | 'FAILED' | 'PENDING';

type AdminLog = {
    id: string;
    time: string;
    actor: 'Admin' | 'System';
    action: string;
    module: string;
    status: LogStatus;
};

const logs: AdminLog[] = [
    {
        id: 'LOG-001',
        time: '2026-06-25 14:22',
        actor: 'System',
        action: 'OpenAlex sync completed',
        module: 'Data Pipeline',
        status: 'SUCCESS',
    },
    {
        id: 'LOG-002',
        time: '2026-06-25 13:40',
        actor: 'Admin',
        action: 'Updated Premium Monthly price',
        module: 'Revenue & Subscription',
        status: 'SUCCESS',
    },
    {
        id: 'LOG-003',
        time: '2026-06-25 12:15',
        actor: 'Admin',
        action: 'Suspended user account',
        module: 'User Governance',
        status: 'SUCCESS',
    },
    {
        id: 'LOG-004',
        time: '2026-06-25 11:20',
        actor: 'System',
        action: 'Scheduled crawl failed',
        module: 'Data Pipeline',
        status: 'FAILED',
    },
];

const AdminLogsPage = () => {
    const [filter, setFilter] = useState<'ALL' | LogStatus>('ALL');

    const filteredLogs =
        filter === 'ALL' ? logs : logs.filter((log) => log.status === filter);

    const successLogs = logs.filter((log) => log.status === 'SUCCESS').length;
    const failedLogs = logs.filter((log) => log.status === 'FAILED').length;

    return (
        <div className="space-y-5">
            <div>
                <h1 className="text-2xl font-extrabold tracking-tight text-slate-950">
                    Activity Logs
                </h1>
                <p className="mt-1 text-xs text-slate-500">
                    Track admin actions and system events across the platform.
                </p>
            </div>

            <div className="grid gap-5 lg:grid-cols-4">
                <AdminMetricCard label="TOTAL LOGS" value={String(logs.length)} helper="All recorded events" icon="▣" accent="blue" />
                <AdminMetricCard label="TODAY LOGS" value={String(logs.length)} helper="Events generated today" icon="◷" accent="slate" />
                <AdminMetricCard label="SUCCESS" value={String(successLogs)} helper="Completed actions" icon="✓" accent="green" />
                <AdminMetricCard label="FAILED" value={String(failedLogs)} helper="Failed system events" icon="!" accent="orange" />
            </div>

            <AdminSectionCard
                title="System & Admin Activity"
                subtitle="Audit trail for system operations and admin actions."
                action={
                    <select
                        value={filter}
                        onChange={(event) => setFilter(event.target.value as 'ALL' | LogStatus)}
                        className="rounded-md border border-slate-200 px-3 py-2 text-xs font-bold text-slate-600"
                    >
                        <option value="ALL">All Status</option>
                        <option value="SUCCESS">Success</option>
                        <option value="FAILED">Failed</option>
                        <option value="PENDING">Pending</option>
                    </select>
                }
            >
                <AdminTable headers={['Log ID', 'Time', 'Actor', 'Action', 'Module', 'Status']}>
                    {filteredLogs.map((log) => (
                        <tr key={log.id} className="hover:bg-slate-50">
                            <td className="px-5 py-4 font-bold text-slate-700">{log.id}</td>
                            <td className="px-5 py-4 text-slate-600">{log.time}</td>
                            <td className="px-5 py-4 font-semibold">{log.actor}</td>
                            <td className="px-5 py-4">{log.action}</td>
                            <td className="px-5 py-4 font-semibold text-slate-700">{log.module}</td>
                            <td className="px-5 py-4">
                                <AdminBadge status={log.status} />
                            </td>
                        </tr>
                    ))}
                </AdminTable>
            </AdminSectionCard>
        </div>
    );
};

export default AdminLogsPage;
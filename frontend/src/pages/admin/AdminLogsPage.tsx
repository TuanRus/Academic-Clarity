import { useEffect, useMemo, useState } from 'react';
import AdminBadge from '../../components/admin/AdminBadge';
import AdminMetricCard from '../../components/admin/AdminMetricCard';
import AdminSectionCard from '../../components/admin/AdminSectionCard';
import AdminTable from '../../components/admin/AdminTable';
import {
    getActivityLogs,
    type ActivityLog,
} from '../../lib/api/admin';

type LogStatus = 'SUCCESS' | 'FAILED' | 'PENDING';

type AdminLogRow = {
    id: string;
    time: string;
    actor: 'Admin' | 'System';
    action: string;
    module: string;
    status: LogStatus;
};

const getLogStatus = (type: ActivityLog['type']): LogStatus => {
    switch (type) {
        case 'AUTH_FAIL':
            return 'FAILED';

        case 'ELEVATION':
        case 'LEDGER':
        case 'UPDATE':
            return 'SUCCESS';

        default:
            return 'PENDING';
    }
};

const getLogActor = (
    type: ActivityLog['type'],
): AdminLogRow['actor'] => {
    switch (type) {
        case 'LEDGER':
        case 'AUTH_FAIL':
            return 'System';

        case 'ELEVATION':
        case 'UPDATE':
            return 'Admin';

        default:
            return 'System';
    }
};

const getLogModule = (type: ActivityLog['type']): string => {
    switch (type) {
        case 'ELEVATION':
            return 'User Governance';

        case 'LEDGER':
            return 'Revenue & Subscription';

        case 'AUTH_FAIL':
            return 'Authentication';

        case 'UPDATE':
            return 'System Administration';

        default:
            return 'System';
    }
};

const AdminLogsPage = () => {
    const [logs, setLogs] = useState<ActivityLog[]>([]);
    const [filter, setFilter] = useState<'ALL' | LogStatus>('ALL');
    const [loading, setLoading] = useState(false);

    const reload = async () => {
        setLoading(true);

        try {
            const data = await getActivityLogs();
            setLogs(data);
        } catch {
            setLogs([]);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        reload();
    }, []);

    const tableLogs = useMemo<AdminLogRow[]>(
        () =>
            logs.map((log, index) => ({
                id: `LOG-${String(index + 1).padStart(3, '0')}`,
                time: log.time,
                actor: getLogActor(log.type),
                action: log.title,
                module: getLogModule(log.type),
                status: getLogStatus(log.type),
            })),
        [logs],
    );

    const filteredLogs = useMemo(
        () =>
            filter === 'ALL'
                ? tableLogs
                : tableLogs.filter((log) => log.status === filter),
        [filter, tableLogs],
    );

    const successLogs = tableLogs.filter(
        (log) => log.status === 'SUCCESS',
    ).length;

    const failedLogs = tableLogs.filter(
        (log) => log.status === 'FAILED',
    ).length;

    const today = new Date().toLocaleDateString('vi-VN');

    const todayLogs = tableLogs.filter((log) => {
        const logDate = new Date(log.time);

        if (Number.isNaN(logDate.getTime())) {
            return false;
        }

        return logDate.toLocaleDateString('vi-VN') === today;
    }).length;

    return (
        <div className="space-y-5">
            <div>
                <h1 className="text-2xl font-bold tracking-tight text-gray-900">
                    Activity Logs
                </h1>

                <p className="mt-1 text-xs text-gray-500">
                    Track administrative actions and system events across the platform.
                </p>
            </div>

            <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4">
                <AdminMetricCard
                    label="TOTAL LOGS"
                    value={String(tableLogs.length)}
                    helper="All recorded events"
                    icon="▣"
                    accent="blue"
                />

                <AdminMetricCard
                    label="TODAY LOGS"
                    value={String(todayLogs)}
                    helper="Events generated today"
                    icon="◷"
                    accent="slate"
                />

                <AdminMetricCard
                    label="SUCCESS"
                    value={String(successLogs)}
                    helper="Completed actions"
                    icon="✓"
                    accent="green"
                />

                <AdminMetricCard
                    label="FAILED"
                    value={String(failedLogs)}
                    helper="Failed system events"
                    icon="!"
                    accent="orange"
                />
            </div>

            <AdminSectionCard
                title="System & Admin Activity"
                subtitle="Audit trail for system operations and administrative actions."
                action={
                    <div className="flex items-center gap-3">
                        <select
                            value={filter}
                            onChange={(event) =>
                                setFilter(event.target.value as 'ALL' | LogStatus)
                            }
                            className="rounded-md border border-gray-200 bg-white px-3 py-2 text-xs font-bold text-gray-600 outline-none focus:border-indigo-700"
                        >
                            <option value="ALL">All Status</option>
                            <option value="SUCCESS">Success</option>
                            <option value="FAILED">Failed</option>
                            <option value="PENDING">Pending</option>
                        </select>

                        <button
                            type="button"
                            onClick={reload}
                            disabled={loading}
                            className="rounded-md border border-gray-200 bg-white px-3 py-2 text-xs font-bold text-gray-600 hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-50"
                        >
                            {loading ? 'Refreshing...' : '↻ Refresh'}
                        </button>
                    </div>
                }
            >
                {loading && tableLogs.length === 0 ? (
                    <p className="p-5 text-sm text-gray-500">
                        Loading activity logs...
                    </p>
                ) : filteredLogs.length === 0 ? (
                    <p className="p-5 text-sm text-gray-400">
                        No activity logs found.
                    </p>
                ) : (
                    <AdminTable
                        headers={[
                            'Log ID',
                            'Time',
                            'Actor',
                            'Action',
                            'Module',
                            'Status',
                        ]}
                    >
                        {filteredLogs.map((log) => (
                            <tr key={log.id} className="hover:bg-gray-50">
                                <td className="px-5 py-4 font-bold text-gray-700">
                                    {log.id}
                                </td>

                                <td className="whitespace-nowrap px-5 py-4 text-gray-600">
                                    {log.time}
                                </td>

                                <td className="px-5 py-4 font-semibold text-gray-700">
                                    {log.actor}
                                </td>

                                <td className="px-5 py-4 text-gray-700">
                                    {log.action}
                                </td>

                                <td className="px-5 py-4 font-semibold text-gray-700">
                                    {log.module}
                                </td>

                                <td className="px-5 py-4">
                                    <AdminBadge status={log.status} />
                                </td>
                            </tr>
                        ))}
                    </AdminTable>
                )}
            </AdminSectionCard>
        </div>
    );
};

export default AdminLogsPage;
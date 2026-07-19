// import { useEffect, useState } from 'react';
// import AdminSectionCard from '../../components/admin/AdminSectionCard';
// import { getActivityLogs, type ActivityLog } from '../../lib/api/admin';

// const logTone: Record<ActivityLog['type'], string> = {
//   ELEVATION: 'border-blue-400 bg-blue-50 text-blue-700',
//   LEDGER: 'border-slate-300 bg-slate-50 text-slate-700',
//   AUTH_FAIL: 'border-red-400 bg-red-50 text-red-700',
//   UPDATE: 'border-sky-400 bg-sky-50 text-sky-700',
// };

// const AdminActivityLogsPage = () => {
//   const [logs, setLogs] = useState<ActivityLog[]>([]);

//   const reload = () => getActivityLogs().then(setLogs).catch(() => setLogs([]));

//   useEffect(() => {
//     reload();
//   }, []);

//   return (
//     <div className="space-y-5">
//       <div>
//         <h1 className="text-2xl font-extrabold tracking-tight text-slate-950">System Activity Logs</h1>
//         <p className="mt-1 text-xs text-slate-500">Audit trail of administrative actions (hiển thị theo giờ Việt Nam).</p>
//       </div>

//       <AdminSectionCard
//         title="Audit Trail"
//         action={<button onClick={reload} className="text-sm text-slate-500 hover:text-slate-700">↻ Refresh</button>}
//       >
//         <div className="space-y-3 p-4">
//           {logs.length === 0 && <p className="text-sm text-slate-400">Chưa có hoạt động nào được ghi nhận.</p>}
//           {logs.map((log, index) => (
//             <div key={`${log.type}-${log.time}-${index}`} className={`rounded border-l-4 p-3 ${logTone[log.type]}`}>
//               <div className="flex items-center justify-between gap-2">
//                 <span className="text-[9px] font-extrabold">{log.type}</span>
//                 <span className="text-[10px] text-slate-500">{log.time}</span>
//               </div>
//               <p className="mt-2 text-xs font-bold text-slate-800">{log.title}</p>
//               <p className="mt-2 text-[10px] text-slate-500">{log.ref}</p>
//             </div>
//           ))}
//         </div>
//       </AdminSectionCard>
//     </div>
//   );
// };

// export default AdminActivityLogsPage;

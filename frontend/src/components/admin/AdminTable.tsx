import type { ReactNode } from 'react';

interface AdminTableProps {
  headers: string[];
  children: ReactNode;
}

const AdminTable = ({ headers, children }: AdminTableProps) => {
  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse text-left text-xs">
        <thead className="bg-slate-50 text-[10px] font-bold uppercase tracking-wide text-slate-500">
          <tr>
            {headers.map((header) => (
              <th key={header} className="px-5 py-3">
                {header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 text-slate-700">{children}</tbody>
      </table>
    </div>
  );
};

export default AdminTable;

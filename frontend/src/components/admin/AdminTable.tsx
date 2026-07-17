import type { ReactNode } from 'react';

interface AdminTableProps {
  headers: string[];
  children: ReactNode;
}

const AdminTable = ({ headers, children }: AdminTableProps) => {
  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse text-left text-sm">
        <thead className="bg-gray-50 text-xs font-medium text-gray-500">
          <tr>
            {headers.map((header) => (
              <th key={header} className="px-5 py-3">
                {header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100 text-gray-700">{children}</tbody>
      </table>
    </div>
  );
};

export default AdminTable;

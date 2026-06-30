import type { ReactNode } from 'react';

interface AdminSectionCardProps {
  title: string;
  subtitle?: string;
  action?: ReactNode;
  children: ReactNode;
}

const AdminSectionCard = ({ title, subtitle, action, children }: AdminSectionCardProps) => {
  return (
    <section className="rounded-lg border border-slate-200 bg-white shadow-sm">
      <div className="flex items-start justify-between gap-4 border-b border-slate-200 px-5 py-4">
        <div>
          <h2 className="text-sm font-bold text-slate-900">{title}</h2>
          {subtitle && <p className="mt-1 text-xs text-slate-500">{subtitle}</p>}
        </div>
        {action}
      </div>
      <div>{children}</div>
    </section>
  );
};

export default AdminSectionCard;

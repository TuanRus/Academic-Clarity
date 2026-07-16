import type { ReactNode } from 'react';

interface AdminSectionCardProps {
  title: string;
  subtitle?: string;
  action?: ReactNode;
  children: ReactNode;
}

const AdminSectionCard = ({ title, subtitle, action, children }: AdminSectionCardProps) => {
  return (
    <section className="rounded-xl border border-gray-200 bg-white shadow-sm">
      <div className="flex items-start justify-between gap-4 border-b border-gray-200 px-5 py-4">
        <div>
          <h2 className="text-base font-semibold text-gray-900">
            {title}
          </h2>

          {subtitle && (
            <p className="mt-1 text-sm text-gray-500">
              {subtitle}
            </p>
          )}
        </div>

        {action}
      </div>

      <div>{children}</div>
    </section>
  );
};

export default AdminSectionCard;

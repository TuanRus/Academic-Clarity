interface AdminMetricCardProps {
  label: string;
  value: string;
  helper: string;
  icon: string;
  accent?: keyof typeof accentMap;
}

const accentMap = {
  blue: {
    icon: 'bg-blue-50 text-blue-700',
    border: 'border-blue-100',
  },
  green: {
    icon: 'bg-emerald-50 text-emerald-700',
    border: 'border-emerald-100',
  },
  orange: {
    icon: 'bg-orange-50 text-orange-700',
    border: 'border-orange-100',
  },
  slate: {
    icon: 'bg-slate-100 text-slate-700',
    border: 'border-slate-200',
  },
  red: {
    icon: 'bg-red-50 text-red-700',
    border: 'border-red-100',
  },
  yellow: {
    icon: 'bg-yellow-50 text-yellow-700',
    border: 'border-yellow-100',
  },
  purple: {
    icon: 'bg-purple-50 text-purple-700',
    border: 'border-purple-100',
  },
  pink: {
    icon: 'bg-pink-50 text-pink-700',
    border: 'border-pink-100',
  },
  cyan: {
    icon: 'bg-cyan-50 text-cyan-700',
    border: 'border-cyan-100',
  },
} as const;

const AdminMetricCard = ({
  label,
  value,
  helper,
  icon,
  accent = 'blue',
}: AdminMetricCardProps) => {
  const styles = accentMap[accent];

  return (
    <div
      className={[
        'rounded-xl border bg-white p-5 shadow-sm transition',
        'hover:-translate-y-0.5 hover:shadow-md',
        styles.border,
      ].join(' ')}
    >
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0 flex-1">
          <p className="text-xs font-extrabold uppercase tracking-[0.06em] text-slate-500">
            {label}
          </p>

          <p className="mt-3 text-2xl font-extrabold leading-none text-slate-950">
            {value}
          </p>

          <p className="mt-3 text-sm leading-5 text-slate-500">
            {helper}
          </p>
        </div>

        <div
          className={[
            'flex h-10 w-10 shrink-0 items-center justify-center rounded-lg',
            'text-base font-extrabold',
            styles.icon,
          ].join(' ')}
          aria-hidden="true"
        >
          {icon}
        </div>
      </div>
    </div>
  );
};

export default AdminMetricCard;
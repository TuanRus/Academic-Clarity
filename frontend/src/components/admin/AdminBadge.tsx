import type { AdminStatus } from '../../lib/api/admin';

const toneMap: Record<AdminStatus, string> = {
  ACTIVE: 'bg-emerald-100 text-emerald-700',
  REQUESTED: 'bg-blue-100 text-blue-700',
  GENERATING: 'bg-amber-100 text-amber-700',
  READY: 'bg-emerald-100 text-emerald-700',
  FAILED: 'bg-red-100 text-red-700',
  SUCCESS: 'bg-emerald-100 text-emerald-700',
  PENDING: 'bg-sky-100 text-sky-700',
  SUSPENDED: 'bg-orange-100 text-orange-700',
  DRAFT: 'bg-orange-100 text-orange-700',
  VERIFIED: 'bg-emerald-100 text-emerald-700',
  REGISTERED: 'bg-blue-100 text-blue-700',
  EXPIRED: 'bg-gray-100 text-gray-600',
  DISMISSED: 'bg-gray-100 text-gray-600',
  REVIEWING: 'bg-purple-100 text-purple-700',
  RUNNING: 'bg-sky-100 text-sky-700',
  CANCELLED: 'bg-gray-100 text-gray-600',
};

interface AdminBadgeProps {
  status: AdminStatus;
  className?: string;
}

const AdminBadge = ({ status, className = '' }: AdminBadgeProps) => {
  return (
    <span className={`inline-flex rounded-full px-2.5 py-1 text-[11px] font-bold ${toneMap[status]} ${className}`}>
      {status}
    </span>
  );
};

export default AdminBadge;

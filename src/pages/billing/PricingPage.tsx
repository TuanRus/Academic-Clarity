import { Link } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { AccessTier, Role } from '../../types/auth';
import { getPremiumMonthlyPrice, formatVnd } from '../../lib/pricing';
import { getRoleLabel } from '../../lib/role';

// MỚI · Pricing Screen
// So sánh quyền BASIC vs PREMIUM theo tier_permissions (mục 7.4 tài liệu quyết định).
const FEATURES: { label: string; basic: boolean; premium: boolean; fr: string }[] = [
  { label: 'Keyword & DOI Search', basic: true, premium: true, fr: 'FR-06..09 / SEARCH_BASIC' },
  { label: 'Topic Network Graph (basic view)', basic: true, premium: true, fr: 'FR-11..14 / GRAPH_BASIC' },
  { label: 'Advanced Topic Insights & Growth View', basic: false, premium: true, fr: 'FR-15 / GRAPH_ADVANCED' },
  { label: 'Publication Volume Trend chart', basic: true, premium: true, fr: 'FR-19 / DASHBOARD_BASIC' },
  { label: 'Trending Keywords + custom date filter', basic: false, premium: true, fr: 'FR-20,21 / DASHBOARD_ADVANCED' },
  { label: 'Export dashboard data to CSV', basic: false, premium: true, fr: 'FR-22 / EXPORT_CSV' },
  { label: 'Bookmark papers & citation alerts', basic: true, premium: true, fr: 'FR-23..26 / BOOKMARK' },
];

const PricingPage = () => {
  const { user } = useAuth();
  const premiumPrice = getPremiumMonthlyPrice(user);

  return (
    <div className="space-y-6">
      <div className="text-center">
        <h1 className="text-2xl font-bold text-gray-900">Choose your plan</h1>
        <p className="text-sm text-gray-500">
          Quyền truy cập tính năng được xác định theo <code>access_tier</code>, không phụ thuộc vào
          academic role của bạn ({user ? getRoleLabel(user.role) : '—'}).
        </p>
      </div>

      <div className="grid gap-6 sm:grid-cols-2">
        {/* BASIC */}
        <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="text-lg font-semibold text-gray-800">Basic</h2>
          <p className="mt-1 text-2xl font-bold text-gray-900">
            0₫<span className="text-sm font-normal text-gray-500"> / tháng</span>
          </p>
          <ul className="mt-4 space-y-2 text-sm">
            {FEATURES.map((f) => (
              <li key={f.label} className={f.basic ? 'text-gray-700' : 'text-gray-400 line-through'}>
                {f.basic ? '✓' : '–'} {f.label}
              </li>
            ))}
          </ul>
          {user?.accessTier === AccessTier.BASIC && (
            <span className="mt-4 inline-block rounded-full bg-gray-100 px-3 py-1 text-xs text-gray-600">
              Current plan
            </span>
          )}
        </div>

        {/* PREMIUM */}
        <div className="rounded-xl border border-indigo-700 bg-white p-6 shadow-sm">
          <h2 className="text-lg font-semibold text-indigo-700">Premium</h2>
          <p className="mt-1 text-2xl font-bold text-gray-900">
            {formatVnd(premiumPrice)}
            <span className="text-sm font-normal text-gray-500"> / tháng</span>
          </p>
          {user?.role === Role.EDU && (
            <span className="mt-1 inline-block rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-700">
              Giá ưu đãi cho tài khoản EDU
            </span>
          )}
          <ul className="mt-4 space-y-2 text-sm text-gray-700">
            {FEATURES.map((f) => (
              <li key={f.label}>✓ {f.label}</li>
            ))}
          </ul>

          {user?.accessTier === AccessTier.PREMIUM ? (
            <span className="mt-4 inline-block rounded-full bg-indigo-50 px-3 py-1 text-xs text-indigo-700">
              Current plan
            </span>
          ) : (
            <Link
              to="/checkout"
              className="mt-4 block rounded-md bg-indigo-700 px-4 py-2 text-center text-sm font-medium text-white hover:bg-indigo-800"
            >
              Upgrade to Premium
            </Link>
          )}
        </div>
      </div>

      <p className="text-center text-xs text-gray-400">
        Ngoại lệ: tài khoản role = ADMIN luôn được coi là PREMIUM cho mọi tính năng sản phẩm (BR-26),
        không cần thanh toán.
      </p>
    </div>
  );
};

export default PricingPage;

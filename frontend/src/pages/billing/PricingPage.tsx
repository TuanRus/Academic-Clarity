import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { AccessTier } from '../../types/auth';
import { getPublicPlans, type PublicPlan } from '../../lib/api/payment';

// MỚI · Pricing Screen
// So sánh quyền BASIC vs PREMIUM theo tier_permissions (mục 7.4 tài liệu quyết định).
const FEATURES: { label: string; basic: boolean; premium: boolean; fr: string }[] = [
  { label: 'Keyword & DOI Search', basic: true, premium: true, fr: 'FR-06..09 / SEARCH_BASIC' },
  { label: 'Topic Network Graph (basic view)', basic: true, premium: true, fr: 'FR-11..14 / GRAPH_BASIC' },
  { label: 'Advanced Topic Insights & Growth View', basic: true, premium: true, fr: 'FR-15 / GRAPH_ADVANCED' },
  { label: 'Publication Volume Trend chart', basic: true, premium: true, fr: 'FR-19 / DASHBOARD_BASIC' },
  { label: 'Trending Keywords + custom date filter', basic: true, premium: true, fr: 'FR-20,21 / DASHBOARD_ADVANCED' },
  { label: 'Export dashboard data to CSV', basic: false, premium: true, fr: 'FR-22 / EXPORT_CSV' },
  { label: 'Idea Overlap Checker', basic: false, premium: true, fr: 'OVERLAP_CHECK' },
  { label: 'Bookmark papers & citation alerts', basic: true, premium: true, fr: 'FR-23..26 / BOOKMARK' },
];

const PricingPage = () => {
  const { user } = useAuth();
  const [plans, setPlans] = useState<PublicPlan[]>([]);

  useEffect(() => {
    getPublicPlans().then(setPlans).catch(() => setPlans([]));
  }, []);

  // Gói rẻ nhất (thường là gói tháng) làm giá hiển thị đại diện trên thẻ Premium.
  const cheapest = plans.length ? plans.reduce((a, b) => (a.priceAmount <= b.priceAmount ? a : b)) : null;
  const premiumPrice = cheapest
    ? `${cheapest.priceAmount.toLocaleString('vi-VN')}₫`
    : '—';

  return (
    <div className="space-y-6">
      <div className="text-center">
        <h1 className="text-2xl font-bold text-gray-900">Choose your plan</h1>
        <p className="text-sm text-gray-500">
          Feature access is determined by your <code>access_tier</code>, independent of your
          academic role ({user?.role}).
        </p>
      </div>

      <div className="grid gap-6 sm:grid-cols-2">
        {/* BASIC */}
        <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="text-lg font-semibold text-gray-800">Basic</h2>
          <p className="mt-1 text-2xl font-bold text-gray-900">
            0₫<span className="text-sm font-normal text-gray-500"> / month</span>
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
            {premiumPrice}
            <span className="text-sm font-normal text-gray-500">
              {cheapest ? ` / ${cheapest.durationDays} ngày` : ' / month'}
            </span>
          </p>
          {plans.length > 1 && (
            <p className="mt-1 text-xs text-gray-500">
              {plans
                .slice()
                .sort((a, b) => a.priceAmount - b.priceAmount)
                .map((p) => `${p.priceAmount.toLocaleString('vi-VN')}₫/${p.durationDays}d`)
                .join(' · ')}
            </p>
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
        {/* BR-26: Admin luôn = PREMIUM cho mọi tính năng sản phẩm, không cần thanh toán */}
        Exception: ADMIN accounts are always treated as PREMIUM for all product features,
        with no payment required.
      </p>
    </div>
  );
};

export default PricingPage;

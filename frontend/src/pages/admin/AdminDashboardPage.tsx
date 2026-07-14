import { useEffect, useState } from 'react';
import { ChevronDown, ChevronUp } from 'lucide-react';
import { getDashboardStats, getDashboardCharts, getUsers, type DashboardStats, type DashboardCharts, type UserDirectoryRow } from '../../lib/api/admin';
import AdminMetricCard from '../../components/admin/AdminMetricCard';
import AdminSectionCard from '../../components/admin/AdminSectionCard';
import AdminToast from '../../components/admin/AdminToast';
import AdminModal from '../../components/admin/AdminModal';

const formatRevenue = (value: number) => `${value.toLocaleString('vi-VN')}₫`;
const formatArticles = (value: number) => value.toLocaleString();
const renewalRate = 81;

// Gắn % tăng trưởng so với tháng trước (cho chart hiển thị).
const withGrowth = (arr: { month: string; value: number }[]) =>
  arr.map((it, i) => {
    const prev = i > 0 ? arr[i - 1].value : 0;
    const g = prev > 0 ? Math.round(((it.value - prev) / prev) * 100) : 0;
    return { ...it, growth: `${g >= 0 ? '+' : ''}${g}%` };
  });

const AdminDashboardPage = () => {
  const [toast, setToast] = useState<string | null>(null);
  // Số liệu THẬT từ BE.
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [charts, setCharts] = useState<DashboardCharts>({ revenue: [], papers: [], plans: [] });
  const [users, setUsers] = useState<UserDirectoryRow[]>([]);
  useEffect(() => {
    getDashboardStats().then(setStats).catch(() => setStats(null));
    getDashboardCharts().then(setCharts).catch(() => setCharts({ revenue: [], papers: [], plans: [] }));
    getUsers().then(setUsers).catch(() => setUsers([]));
  }, []);

  // Dữ liệu chart & tổng hợp — DERIVE từ API thật (giữ nguyên tên biến để JSX không phải đổi).
  const revenueTrend = withGrowth(charts.revenue);
  const publicationTrend = withGrowth(charts.papers);
  const premiumUsers = charts.plans.reduce((s, p) => s + p.count, 0);
  const totalUsers = users.length;
  const adminUsers = users.filter((u) => u.role === 'ADMIN').length;
  const userOverview = { totalUsers, premiumUsers, freeUsers: Math.max(0, totalUsers - premiumUsers), adminUsers };
  const totalRevenue = revenueTrend.reduce((sum, item) => sum + item.value, 0);
  const monthlyRevenue = revenueTrend.length ? revenueTrend[revenueTrend.length - 1].value : 0;
  const activePremium = premiumUsers;
  const totalPublications = publicationTrend.reduce((sum, item) => sum + item.value, 0);
  const monthlyPublications = publicationTrend.length ? publicationTrend[publicationTrend.length - 1].value : 0;
  const peakPublicationGrowth = publicationTrend.length ? publicationTrend[publicationTrend.length - 1].growth : '—';
  const latestPublicationMonth = publicationTrend.length ? publicationTrend[publicationTrend.length - 1].month : '—';
  const [showRevenueDetail, setShowRevenueDetail] = useState(false);
  const [showPublicationDetail, setShowPublicationDetail] = useState(false);

  const [collapsedSections, setCollapsedSections] = useState({
    publicationTrend: false,
    revenueTrend: false,
    userOverview: false,
    subscriptionDistribution: false,
  });

  const toggleSection = (section: keyof typeof collapsedSections) => {
    setCollapsedSections((current) => ({
      ...current,
      [section]: !current[section],
    }));
  };

  const collapseButton = (section: keyof typeof collapsedSections) => (
    <button
      type="button"
      onClick={() => toggleSection(section)}
      className="flex h-8 w-8 items-center justify-center rounded-full bg-slate-100 text-slate-500 transition hover:bg-[#4338ca] hover:text-white"
    >
      {collapsedSections[section] ? (
        <ChevronDown size={18} strokeWidth={2.5} />
      ) : (
        <ChevronUp size={18} strokeWidth={2.5} />
      )}
    </button>
  );

  const collapseClass = (
    section: keyof typeof collapsedSections,
    openHeight: string,
  ) =>
    `overflow-hidden transition-all duration-300 ease-in-out ${
      collapsedSections[section] ? 'max-h-0 opacity-0' : `${openHeight} opacity-100`
    }`;

  return (
    <div className="space-y-5">
      <AdminToast message={toast} onClose={() => setToast(null)} />

      <div>
        <h1 className="text-2xl font-extrabold tracking-tight text-slate-950">
          Admin Dashboard Overview
        </h1>
        <p className="mt-1 text-xs text-slate-500">
          Overview of the system's articles, revenue and subscription metrics.
        </p>
      </div>

      <div className="grid gap-5 lg:grid-cols-4">
        <AdminMetricCard
          label="TOTAL ARTICLES"
          value={stats ? stats.totalPapers.toLocaleString() : '—'}
          helper="Articles stored in system"
          icon="📄"
          accent="blue"
        />

        <AdminMetricCard
          label="TOTAL REVENUE"
          value={stats ? `${stats.totalRevenue.toLocaleString('vi-VN')}₫` : '—'}
          helper="Revenue from subscriptions"
          icon="₫"
          accent="green"
        />

        <AdminMetricCard
          label="TOTAL AUTHORS"
          value={stats ? stats.totalAuthors.toLocaleString() : '—'}
          helper="Distinct authors in corpus"
          icon="👥"
          accent="slate"
        />

        <AdminMetricCard
          label="ACTIVE SUBSCRIPTIONS"
          value={stats ? stats.activeSubscriptions.toLocaleString() : '—'}
          helper="Premium Monthly & Yearly"
          icon="★"
          accent="orange"
        />
      </div>

      <div className="grid items-start gap-5 xl:grid-cols-2">
        <AdminSectionCard
          title="Publication Trend"
          subtitle="Number of publications by month"
          action={collapseButton('publicationTrend')}
        >
          <div className={collapseClass('publicationTrend', 'max-h-[430px]')}>
            <div className="p-6">
              <div className="mb-5 flex items-end justify-between">
                <div>
                  <p className="text-2xl font-extrabold text-slate-950">
                    {formatArticles(totalPublications)}
                  </p>
                  <p className="text-xs font-semibold text-slate-500">
                    Publications in last 6 months
                  </p>
                </div>

                <span className="rounded-full bg-emerald-50 px-3 py-1 text-xs font-bold text-emerald-700">
                  +28% peak growth
                </span>
              </div>

              <button
                type="button"
                onClick={() => setShowPublicationDetail(true)}
                className="flex h-64 w-full cursor-pointer items-end gap-4 rounded-xl bg-slate-50 p-5 text-left transition hover:bg-slate-100"
              >
                {publicationTrend.map((item) => (
                  <div
                    key={item.month}
                    className="group relative flex flex-1 flex-col items-center gap-3"
                  >
                    <div
                      className="w-full max-w-[76px] rounded-t-xl bg-[#160078] transition hover:opacity-80"
                      style={{ height: `${Math.min(item.value / 4, 220)}px` }}
                    />

                    <div className="pointer-events-none absolute bottom-16 z-10 hidden rounded-lg bg-slate-950 px-3 py-2 text-xs text-white shadow-lg group-hover:block">
                      <p className="font-bold">{item.month}</p>
                      <p>{formatArticles(item.value)} publications</p>
                      <p className="text-emerald-300">
                        {item.growth} vs previous month
                      </p>
                    </div>

                    <span className="text-xs font-semibold text-slate-500">
                      {item.month}
                    </span>
                  </div>
                ))}
              </button>
            </div>
          </div>
        </AdminSectionCard>

        <AdminSectionCard
          title="Revenue Trend"
          subtitle="Revenue analytics and subscription performance"
          action={collapseButton('revenueTrend')}
        >
          <div className={collapseClass('revenueTrend', 'max-h-[430px]')}>
            <div className="p-6">
              <div className="mb-5 flex items-end justify-between">
                <div>
                  <p className="text-2xl font-extrabold text-slate-950">
                    {formatRevenue(totalRevenue)}
                  </p>
                  <p className="text-xs font-semibold text-slate-500">
                    Revenue in last 6 months
                  </p>
                </div>

                <span className="rounded-full bg-emerald-50 px-3 py-1 text-xs font-bold text-emerald-700">
                  +20% peak growth
                </span>
              </div>

              <button
                type="button"
                onClick={() => setShowRevenueDetail(true)}
                className="flex h-64 w-full cursor-pointer items-end gap-4 rounded-xl bg-slate-50 p-5 text-left transition hover:bg-slate-100"
              >
                {revenueTrend.map((item) => (
                  <div
                    key={item.month}
                    className="group relative flex flex-1 flex-col items-center gap-3"
                  >
                    <div
                      className="w-full max-w-[76px] rounded-t-xl bg-emerald-500 transition hover:opacity-80"
                      style={{ height: `${Math.min(item.value * 7, 220)}px` }}
                    />

                    <div className="pointer-events-none absolute bottom-16 z-10 hidden rounded-lg bg-slate-950 px-3 py-2 text-xs text-white shadow-lg group-hover:block">
                      <p className="font-bold">{item.month}</p>
                      <p>{formatRevenue(item.value)} revenue</p>
                      <p className="text-emerald-300">
                        {item.growth} vs previous month
                      </p>
                    </div>

                    <span className="text-xs font-semibold text-slate-500">
                      {item.month}
                    </span>
                  </div>
                ))}
              </button>
            </div>
          </div>
        </AdminSectionCard>
      </div>

      <AdminSectionCard
        title="User Overview"
        subtitle="User account distribution by access level"
        action={collapseButton('userOverview')}
      >
        <div className={collapseClass('userOverview', 'max-h-[340px]')}>
          <div className="grid gap-6 p-6 md:grid-cols-[220px_1fr]">
            <div className="relative mx-auto flex h-44 w-44 items-center justify-center rounded-full bg-[conic-gradient(#160078_0_67%,#10b981_67%_99%,#fb923c_99%_100%)]">
              <div className="flex h-28 w-28 flex-col items-center justify-center rounded-full bg-white shadow-inner">
                <span className="text-2xl font-extrabold text-slate-950">
                  {userOverview.totalUsers.toLocaleString()}
                </span>
                <span className="text-xs font-semibold text-slate-500">
                  Total Users
                </span>
              </div>
            </div>

            <div className="space-y-4">
              <div className="flex items-center justify-between rounded-xl bg-slate-50 px-4 py-3">
                <span className="font-semibold text-slate-700">Total Users</span>
                <span className="font-extrabold text-slate-950">
                  {userOverview.totalUsers.toLocaleString()}
                </span>
              </div>

              <div className="flex items-center justify-between rounded-xl bg-slate-50 px-4 py-3">
                <span className="flex items-center gap-2 font-semibold text-slate-700">
                  <span className="h-3 w-3 rounded-full bg-[#160078]" />
                  Premium
                </span>
                <span className="font-extrabold text-slate-950">
                  {userOverview.premiumUsers.toLocaleString()}
                </span>
              </div>

              <div className="flex items-center justify-between rounded-xl bg-slate-50 px-4 py-3">
                <span className="flex items-center gap-2 font-semibold text-slate-700">
                  <span className="h-3 w-3 rounded-full bg-emerald-500" />
                  Free
                </span>
                <span className="font-extrabold text-slate-950">
                  {userOverview.freeUsers.toLocaleString()}
                </span>
              </div>

              <div className="flex items-center justify-between rounded-xl bg-slate-50 px-4 py-3">
                <span className="flex items-center gap-2 font-semibold text-slate-700">
                  <span className="h-3 w-3 rounded-full bg-orange-400" />
                  Admin
                </span>
                <span className="font-extrabold text-slate-950">
                  {userOverview.adminUsers}
                </span>
              </div>
            </div>
          </div>
        </div>
      </AdminSectionCard>

      <AdminSectionCard
        title="Subscription Distribution"
        subtitle="Current subscription plan allocation"
        action={collapseButton('subscriptionDistribution')}
      >
        <div className={collapseClass('subscriptionDistribution', 'max-h-[260px]')}>
          <div className="space-y-5 p-6">
            <div>
              <div className="mb-2 flex justify-between text-sm font-semibold">
                <span>Premium Monthly</span>
                <span>70%</span>
              </div>
              <div className="h-3 rounded-full bg-slate-100">
                <div
                  className="h-3 rounded-full bg-[#160078]"
                  style={{ width: '70%' }}
                />
              </div>
            </div>

            <div>
              <div className="mb-2 flex justify-between text-sm font-semibold">
                <span>Premium Yearly</span>
                <span>20%</span>
              </div>
              <div className="h-3 rounded-full bg-slate-100">
                <div
                  className="h-3 rounded-full bg-emerald-500"
                  style={{ width: '20%' }}
                />
              </div>
            </div>

            <div>
              <div className="mb-2 flex justify-between text-sm font-semibold">
                <span>Free Users</span>
                <span>10%</span>
              </div>
              <div className="h-3 rounded-full bg-slate-100">
                <div
                  className="h-3 rounded-full bg-orange-400"
                  style={{ width: '10%' }}
                />
              </div>
            </div>
          </div>
        </div>
      </AdminSectionCard>

      <AdminModal
        open={showPublicationDetail}
        title="Publication Trend Detail"
        subtitle="Detailed publication performance from the last 6 months."
        onClose={() => setShowPublicationDetail(false)}
      >
        <div className="space-y-5">
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <p className="text-xs font-bold text-slate-500">Total Publications</p>
              <p className="mt-1 text-2xl font-extrabold text-slate-950">
                {formatArticles(totalPublications)}
              </p>
            </div>

            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <p className="text-xs font-bold text-slate-500">
                Monthly Publications
              </p>
              <p className="mt-1 text-2xl font-extrabold text-slate-950">
                {formatArticles(monthlyPublications)}
              </p>
            </div>

            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <p className="text-xs font-bold text-slate-500">Peak Growth</p>
              <p className="mt-1 text-2xl font-extrabold text-emerald-700">
                {peakPublicationGrowth}
              </p>
            </div>

            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <p className="text-xs font-bold text-slate-500">Latest Month</p>
              <p className="mt-1 text-2xl font-extrabold text-[#4338ca]">
                {latestPublicationMonth}
              </p>
            </div>
          </div>

          <div className="rounded-xl bg-slate-50 p-4">
            <p className="mb-3 text-sm font-bold text-slate-900">
              Monthly Publication Breakdown
            </p>

            <div className="space-y-3">
              {publicationTrend.map((item) => (
                <div key={item.month}>
                  <div className="mb-1 flex justify-between text-xs font-semibold text-slate-600">
                    <span>{item.month}</span>
                    <span>
                      {formatArticles(item.value)} publications · {item.growth}
                    </span>
                  </div>

                  <div className="h-2 rounded-full bg-slate-200">
                    <div
                      className="h-2 rounded-full bg-[#160078]"
                      style={{ width: `${Math.min(item.value / 6, 100)}%` }}
                    />
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </AdminModal>

      <AdminModal
        open={showRevenueDetail}
        title="Revenue Trend Detail"
        subtitle="Detailed revenue performance from the last 6 months."
        onClose={() => setShowRevenueDetail(false)}
      >
        <div className="space-y-5">
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <p className="text-xs font-bold text-slate-500">Total Revenue</p>
              <p className="mt-1 text-2xl font-extrabold text-slate-950">
                {formatRevenue(totalRevenue)}
              </p>
            </div>

            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <p className="text-xs font-bold text-slate-500">Monthly Revenue</p>
              <p className="mt-1 text-2xl font-extrabold text-slate-950">
                {formatRevenue(monthlyRevenue)}
              </p>
            </div>

            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <p className="text-xs font-bold text-slate-500">Active Premium</p>
              <p className="mt-1 text-2xl font-extrabold text-emerald-700">
                {activePremium}
              </p>
            </div>

            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <p className="text-xs font-bold text-slate-500">Renewal Rate</p>
              <p className="mt-1 text-2xl font-extrabold text-[#4338ca]">
                {renewalRate}%
              </p>
            </div>
          </div>

          <div className="rounded-xl bg-slate-50 p-4">
            <p className="mb-3 text-sm font-bold text-slate-900">
              Monthly Revenue Breakdown
            </p>

            <div className="space-y-3">
              {revenueTrend.map((item) => (
                <div key={item.month}>
                  <div className="mb-1 flex justify-between text-xs font-semibold text-slate-600">
                    <span>{item.month}</span>
                    <span>
                      {formatRevenue(item.value)} · {item.growth}
                    </span>
                  </div>

                  <div className="h-2 rounded-full bg-slate-200">
                    <div
                      className="h-2 rounded-full bg-[#4338ca]"
                      style={{ width: `${Math.min(item.value * 4, 100)}%` }}
                    />
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </AdminModal>
    </div>
  );
};

export default AdminDashboardPage;
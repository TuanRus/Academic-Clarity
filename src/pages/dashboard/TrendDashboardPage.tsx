import RequireFeature from '../../routes/RequireFeature';
import { useFeature } from '../../hooks/useFeature';
import { FeaturePermission } from '../../types/permissions';

// Journal & Keywords · Trend Analytics Dashboard - FR-19..FR-22
// FR-19 (Line Chart cơ bản) -> DASHBOARD_BASIC.
// FR-20/21 (Stacked Bar Chart trending keywords + date filter nâng cao) -> DASHBOARD_ADVANCED.
// FR-22 (Export CSV) -> EXPORT_CSV (permission riêng, không gộp vào DASHBOARD_ADVANCED).
const TrendDashboardPage = () => {
  const canExportCsv = useFeature(FeaturePermission.EXPORT_CSV);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-gray-400">
            Journal & Keywords · Trend Analytics Dashboard
          </p>
          <h1 className="text-2xl font-bold text-gray-900">Basic Trend Dashboard</h1>
        </div>

        {/* FR-22: nút Export chỉ enable khi có EXPORT_CSV (PREMIUM hoặc ADMIN) */}
        <button
          disabled={!canExportCsv}
          title={!canExportCsv ? 'Nâng cấp Premium để xuất CSV (FR-22)' : undefined}
          className={`flex items-center gap-2 rounded-md px-4 py-2 text-sm font-medium ${
            canExportCsv
              ? 'bg-indigo-700 text-white hover:bg-indigo-800'
              : 'cursor-not-allowed bg-gray-200 text-gray-400'
          }`}
        >
          ⬇ Export Data (CSV)
        </button>
      </div>

      {/* FR-19: Publication Volume Trend - DASHBOARD_BASIC */}
      <RequireFeature feature={FeaturePermission.DASHBOARD_BASIC} featureLabel="Publication Volume Trend">
        <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="text-lg font-semibold text-gray-800">Publication Volume Trend</h2>
          <p className="text-sm text-gray-500">Year-over-year global publication count.</p>
          <div className="mt-4 flex h-40 items-center justify-center text-sm text-gray-400">
            [Line chart 2015 → 2025]
          </div>
        </div>
      </RequireFeature>

      {/* FR-20/21: Top Trending Keywords + date filter nâng cao - DASHBOARD_ADVANCED */}
      <RequireFeature
        feature={FeaturePermission.DASHBOARD_ADVANCED}
        featureLabel="Top Trending Keywords"
        description="Mở khoá Stacked Bar Chart các từ khoá nổi bật và bộ lọc năm tuỳ chỉnh khi nâng cấp Premium."
      >
        <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <h2 className="text-lg font-semibold text-gray-800">Top Trending Keywords</h2>
          <p className="text-sm text-gray-500">Highest volume academic concepts by frequency.</p>
          <div className="mt-4 space-y-2 text-sm text-gray-700">
            {['neural networks', 'transformers', 'gradient descent', 'reinforcement learning'].map((kw) => (
              <div key={kw} className="h-2 rounded bg-indigo-700" style={{ width: `${60 + kw.length}%` }} />
            ))}
          </div>
        </div>
      </RequireFeature>
    </div>
  );
};

export default TrendDashboardPage;

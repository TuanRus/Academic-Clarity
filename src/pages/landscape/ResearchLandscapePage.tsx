import RequireFeature from '../../routes/RequireFeature';
import { FeaturePermission } from '../../types/permissions';

// Research Landscape · Topic Network Graph - FR-11..FR-15
// FR-11 (render graph cơ bản), FR-12/13 (size/color mapping), FR-14 (zoom/pan)
// đều thuộc GRAPH_BASIC -> mọi user (BASIC/PREMIUM/ADMIN) đều xem được.
// FR-15 (side panel chi tiết + Growth View / phân tích sâu) -> GRAPH_ADVANCED (PREMIUM only).
const ResearchLandscapePage = () => {
  return (
    <div className="space-y-4">
      <div>
        <p className="text-xs font-medium uppercase tracking-wide text-gray-400">
          Research Landscape · Topic Network Graph
        </p>
        <h1 className="text-2xl font-bold text-gray-900">Research Topic Landscape</h1>
      </div>

      {/* FR-11..14: GRAPH_BASIC - luôn hiển thị */}
      <RequireFeature feature={FeaturePermission.GRAPH_BASIC} featureLabel="Topic Network Graph">
        <div className="flex h-72 items-center justify-center rounded-xl border border-gray-200 bg-white shadow-sm">
          <p className="text-sm text-gray-400">
            [Force-directed graph: Computer Science → Machine Learning → NLP, ...]
          </p>
        </div>
      </RequireFeature>

      {/* FR-15 + phân tích nâng cao: GRAPH_ADVANCED - chỉ PREMIUM/ADMIN */}
      <RequireFeature
        feature={FeaturePermission.GRAPH_ADVANCED}
        featureLabel="Advanced Topic Insights"
        description="Mở khoá side panel chi tiết (papers, growth %, trending keywords) và Growth View khi nâng cấp Premium."
      >
        <div className="grid gap-4 rounded-xl border border-gray-200 bg-white p-6 shadow-sm sm:grid-cols-3">
          <div>
            <p className="text-xs text-gray-500">Papers</p>
            <p className="text-lg font-semibold text-gray-900">284k</p>
          </div>
          <div>
            <p className="text-xs text-gray-500">Growth</p>
            <p className="text-lg font-semibold text-green-600">+22%</p>
          </div>
          <div>
            <p className="text-xs text-gray-500">Total Citations</p>
            <p className="text-lg font-semibold text-gray-900">1.2M</p>
          </div>
        </div>
      </RequireFeature>
    </div>
  );
};

export default ResearchLandscapePage;

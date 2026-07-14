import { Link } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';

// S-03 · Welcome / Home Screen (FR-03)
const HomePage = () => {
  const { user } = useAuth();
  if (!user) return null;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Welcome back, {user.fullName}!</h1>
        <p className="text-sm text-gray-500">
          Academic Year 2025 - 2026 · Institutional Research & Trend Analytics Workspace
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <Link
          to="/dashboard"
          className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm transition hover:border-indigo-700"
        >
          <h2 className="text-lg font-semibold text-gray-800">Trend Dashboard</h2>
          <p className="mt-1 text-sm text-gray-500">
            Analyze research trends, track emerging fields, and view publication statistics over time.
          </p>
        </Link>

        <Link
          to="/library"
          className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm transition hover:border-indigo-700"
        >
          <h2 className="text-lg font-semibold text-gray-800">Saved Library & Insights</h2>
          <p className="mt-1 text-sm text-gray-500">
            Monitor your bookmarked papers, curate keyword lists, and review personalized trend metrics.
          </p>
        </Link>
      </div>

      <div className="rounded-xl bg-indigo-700 p-5 text-sm text-white">
        💡 Tailor your workspace view by toggling specific journal filters or setting up automated
        keyword alerts directly from your Saved Library.
      </div>
    </div>
  );
};

export default HomePage;

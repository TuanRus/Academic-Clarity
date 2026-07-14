import { Link } from 'react-router-dom';

interface Props {
  /** Tên hiển thị của tính năng bị khoá, ví dụ "Advanced Topic Graph" */
  featureLabel: string;
  description?: string;
}

/**
 * Hiển thị thay cho nội dung Premium khi user chưa có quyền (BR-26).
 * Dùng bên trong <RequireFeature>.
 */
const UpgradeOverlay = ({ featureLabel, description }: Props) => {
  return (
    <div className="flex flex-col items-center justify-center gap-3 rounded-xl border border-gray-200 bg-white p-10 text-center shadow-sm">
      <span className="rounded-full bg-indigo-50 px-3 py-1 text-xs font-medium text-indigo-700">
        Premium Feature
      </span>
      <h3 className="text-lg font-semibold text-gray-900">{featureLabel}</h3>
      <p className="max-w-md text-sm text-gray-500">
        {description ??
          'Tính năng này chỉ dành cho tài khoản Premium. Nâng cấp để mở khoá đồ thị nâng cao, dashboard chi tiết và xuất dữ liệu CSV.'}
      </p>
      <Link
        to="/pricing"
        className="mt-2 rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800"
      >
        Upgrade to Premium
      </Link>
    </div>
  );
};

export default UpgradeOverlay;

import { useAuth } from './useAuth';
import { hasFeature } from '../lib/permissions';
import { FeaturePermission } from '../types/permissions';

/**
 * Hook FE tương đương hasFeature() ở backend.
 * Dùng trực tiếp khi cần if/else đơn giản, hoặc dùng <RequireFeature> khi cần
 * hiển thị UpgradeOverlay cho cả block UI.
 */
export function useFeature(feature: FeaturePermission): boolean {
  const { user } = useAuth();
  return hasFeature(user, feature);
}

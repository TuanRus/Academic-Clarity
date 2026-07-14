import { AccessTier, Role, type User } from '../types/auth';
import { FeaturePermission } from '../types/permissions';

/**
 * Mirror 1:1 của bảng tier_permissions ở backend.
 * Đây là NGUỒN SỰ THẬT DUY NHẤT cho feature access trên FE -
 * mọi nơi cần check quyền PHẢI đi qua hasFeature() / useFeature(), không tự suy luận từ role.
 */
const TIER_PERMISSIONS: Record<AccessTier, FeaturePermission[]> = {
  [AccessTier.BASIC]: [
    FeaturePermission.SEARCH_BASIC,
    FeaturePermission.GRAPH_BASIC,
    FeaturePermission.DASHBOARD_BASIC,
    FeaturePermission.BOOKMARK,
  ],
  [AccessTier.PREMIUM]: [
    FeaturePermission.SEARCH_BASIC,
    FeaturePermission.GRAPH_BASIC,
    FeaturePermission.GRAPH_ADVANCED,
    FeaturePermission.DASHBOARD_BASIC,
    FeaturePermission.DASHBOARD_ADVANCED,
    FeaturePermission.BOOKMARK,
    FeaturePermission.EXPORT_CSV,
  ],
};

/**
 * Mirror của backend hasFeature(user, featurePermissionCode):
 *   if (user.role == ADMIN) return true;          // ngoại lệ duy nhất - BR-26
 *   return tierPermissionRepository.exists(...)
 *
 * Dùng cho FR-11..FR-22 (đồ thị nâng cao, dashboard nâng cao, export CSV...).
 */
export function hasFeature(user: User | null, feature: FeaturePermission): boolean {
  if (!user) return false;
  if (user.role === Role.ADMIN) return true; // Admin = auto PREMIUM cho mọi tính năng sản phẩm
  return TIER_PERMISSIONS[user.accessTier].includes(feature);
}

/**
 * FR-27/28: quyền vào khu vực quản trị chỉ dựa vào role = ADMIN,
 * độc lập hoàn toàn với accessTier.
 */
export function isAdmin(user: User | null): boolean {
  return user?.role === Role.ADMIN;
}

import type { ReactNode } from 'react';
import { useFeature } from '../hooks/useFeature';
import type { FeaturePermission } from '../types/permissions';
import UpgradeOverlay from '../components/UpgradeOverlay';

interface Props {
  feature: FeaturePermission;
  featureLabel: string;
  description?: string;
  children: ReactNode;
}

/**
 * Wrapper (KHÔNG phải route guard) - bọc quanh một block UI thuộc FR-11..22.
 * - Nếu user CÓ quyền (theo access_tier, hoặc role=ADMIN - BR-26) -> render children.
 * - Nếu KHÔNG -> render <UpgradeOverlay> thay thế.
 *
 * Dùng nhiều lần trong cùng 1 page để gate từng phần (ví dụ: graph cơ bản luôn hiện,
 * chỉ phần "Growth View nâng cao" mới bị khoá).
 */
const RequireFeature = ({ feature, featureLabel, description, children }: Props) => {
  const allowed = useFeature(feature);

  if (!allowed) {
    return <UpgradeOverlay featureLabel={featureLabel} description={description} />;
  }

  return <>{children}</>;
};

export default RequireFeature;

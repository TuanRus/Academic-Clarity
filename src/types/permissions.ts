// Permission code tương ứng với tier_permissions ở backend.
// Seed data (theo tài liệu quyết định, mục 7.4):
//   BASIC   = SEARCH_BASIC, GRAPH_BASIC, DASHBOARD_BASIC, BOOKMARK
//   PREMIUM = BASIC + GRAPH_ADVANCED, DASHBOARD_ADVANCED, EXPORT_CSV
export const FeaturePermission = {
  SEARCH_BASIC: 'SEARCH_BASIC',
  GRAPH_BASIC: 'GRAPH_BASIC',
  GRAPH_ADVANCED: 'GRAPH_ADVANCED',
  DASHBOARD_BASIC: 'DASHBOARD_BASIC',
  DASHBOARD_ADVANCED: 'DASHBOARD_ADVANCED',
  EXPORT_CSV: 'EXPORT_CSV',
  BOOKMARK: 'BOOKMARK',
} as const;
export type FeaturePermission = typeof FeaturePermission[keyof typeof FeaturePermission];

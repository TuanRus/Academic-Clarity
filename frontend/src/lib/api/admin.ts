import { apiGet, apiPost, apiPut, apiPatch, apiDelete } from '../http';

// Tầng service cho khu admin — nối các trang admin vào BE thật (bỏ mock).

export type AdminStatus =
  | 'ACTIVE' | 'REQUESTED' | 'GENERATING' | 'READY' | 'FAILED' | 'SUCCESS' | 'PENDING'
  | 'SUSPENDED' | 'DRAFT' | 'VERIFIED' | 'REGISTERED' | 'EXPIRED' | 'DISMISSED' | 'REVIEWING'
  | 'RUNNING' | 'CANCELLED';

export interface ExportRequest { id: string; user: string; type: 'CSV' | 'PDF' | 'XLSX'; timestamp: string; status: AdminStatus; }
export interface PipelineEvent { id?: number; title: string; time: string; status: AdminStatus; recordsImported?: number; }
export interface RepositoryCategory { id: string; name: string; description: string; fields: number; status: AdminStatus; }
export interface RepositoryPaper { id: string; title: string; doi: string; journal: string; year: number; citations: number; status: AdminStatus; }
export interface RepositoryAnomaly { id: string; label: string; title: string; tone: 'orange' | 'red'; action: 'Auto-Fill' | 'Review'; status: AdminStatus; }
export interface UserDirectoryRow { id: string; initials: string; name: string; email: string; role: string; status: AdminStatus; }
export interface ActivityLog { type: 'ELEVATION' | 'LEDGER' | 'AUTH_FAIL' | 'UPDATE'; time: string; title: string; ref: string; }
export interface RevenueRow { invoiceId: string; customer: string; plan: string; amount: string; method: string; paidAt: string; status: AdminStatus; }
export interface SubscriptionPlan { id: string; name: string; price: string; duration: string; status: AdminStatus; priceAmount: number; durationDays: number; }

export interface DashboardStats {
  totalPapers: number; totalAuthors: number; totalRevenue: number;
  activeSubscriptions: number; newPapersThisWeek: number;
}

interface Paged<T> { items: T[]; totalCount: number; page: number; pageSize: number; totalPages: number; }

const initials = (name: string) =>
  (name || '?').split(/\s+/).map((p) => p[0]).slice(0, 2).join('').toUpperCase();
// Định dạng mốc thời gian UTC từ BE sang giờ Việt Nam (UTC+7).
const toVnTime = (utc: string): string => {
  if (!utc) return '';
  // Chuỗi từ BE có thể thiếu hậu tố 'Z' (UTC) → ép coi là UTC để không lệch theo máy client.
  const iso = /[zZ]|[+-]\d{2}:\d{2}$/.test(utc) ? utc : `${utc}Z`;
  const d = new Date(iso);
  if (isNaN(d.getTime())) return utc;
  return d.toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour12: false });
};

const roleName = (roleId: number) =>
  ({ 1: 'ADMIN', 2: 'RESEARCHER', 3: 'STUDENT', 4: 'Member' } as Record<number, string>)[roleId] ?? `ROLE ${roleId}`;

/** Map nhãn role (do roleName trả) ngược về RoleId của BE. */
export const roleIdFromName = (name: string): number =>
  ({ ADMIN: 1, RESEARCHER: 2, STUDENT: 3, Member: 4 } as Record<string, number>)[name] ?? 4;

/** PUT /admin/users/{id}/role — đổi vai trò user (lưu DB). */
export function updateUserRole(userId: string, roleId: number): Promise<unknown> {
  return apiPut(`/admin/users/${userId}/role`, { roleId });
}

/** PATCH /admin/users/{id}/status?isActive= — suspend/activate user (lưu DB). */
export function updateUserStatus(userId: string, isActive: boolean): Promise<unknown> {
  return apiPatch(`/admin/users/${userId}/status?isActive=${isActive}`);
}

// ---- Dashboard ----
export function getDashboardStats(): Promise<DashboardStats> {
  return apiGet<DashboardStats>('/admin/dashboard/stats');
}

// ---- Users directory (GET /admin/users) ----
interface BeUser { userId: number; email: string; fullname: string; roleId: number; isActive: boolean; }
export async function getUsers(): Promise<UserDirectoryRow[]> {
  const res = await apiGet<Paged<BeUser>>('/admin/users', { page: 1, pageSize: 100 });
  return res.items.map((u) => ({
    id: String(u.userId),
    initials: initials(u.fullname),
    name: u.fullname,
    email: u.email,
    role: roleName(u.roleId),
    status: u.isActive ? 'ACTIVE' : 'SUSPENDED',
  }));
}

// ---- Activity logs (GET /admin/activity-logs) ----
interface BeActivityLog { logId: number; adminName: string; action: string; description: string; createdAt: string; }
export async function getActivityLogs(): Promise<ActivityLog[]> {
  const res = await apiGet<Paged<BeActivityLog>>('/admin/activity-logs', { page: 1, pageSize: 50 });
  return res.items.map((l) => ({
    type: l.action?.includes('RESET') || l.action?.includes('DELETE') ? 'AUTH_FAIL'
      : l.action?.includes('REPROCESS') || l.action?.includes('REBUILD') ? 'UPDATE'
      : l.action?.includes('SUBSCRIPTION') || l.action?.includes('REVENUE') ? 'LEDGER' : 'ELEVATION',
    time: toVnTime(l.createdAt),
    title: l.description,
    ref: `${l.action} · ${l.adminName}`,
  }));
}

// ---- Sync pipeline logs (GET /admin/sync-logs) ----
interface BeSyncLog { syncLogId: number; syncStartedAt: string; syncFinishedAt?: string; status: string; recordsImported: number; errorMessage?: string; }
function mapSyncStatus(s: string): AdminStatus {
  switch (s) {
    case 'success': return 'SUCCESS';
    case 'failed': return 'FAILED';
    case 'cancelled': return 'CANCELLED';
    case 'running': return 'RUNNING';
    default: return 'GENERATING';
  }
}
export async function getSyncLogs(): Promise<PipelineEvent[]> {
  const res = await apiGet<Paged<BeSyncLog>>('/admin/sync-logs', { page: 1, pageSize: 50 });
  return res.items.map((s) => ({
    id: s.syncLogId,
    title: `Sync OpenAlex — ${s.recordsImported} bài`,
    time: s.syncStartedAt,
    status: mapSyncStatus(s.status),
    recordsImported: s.recordsImported,
  }));
}

// ---- Sync realtime (chạy nền + poll tiến độ) ----
export interface SyncProgressEntry { time: string; title: string; status: string; }
export interface SyncProgress {
  isRunning: boolean; syncLogId: number | null;
  added: number; exists: number; errors: number; total: number;
  entries: SyncProgressEntry[];
}
export async function startLiveSync(maxPages = 2): Promise<{ started: boolean; maxPages: number }> {
  return apiPost(`/admin/sync/start?maxPages=${maxPages}`) as Promise<{ started: boolean; maxPages: number }>;
}
export async function getSyncProgress(): Promise<SyncProgress> {
  return apiGet<SyncProgress>('/admin/sync/progress');
}

// ---- Chi tiết bài đã sync (GET /admin/sync-logs/{id}/papers) ----
export interface SyncedPaper { paperId: string; title: string; publicationYear: number | null; openAlexId?: string; sourceUrl?: string; createdAt: string; }
export async function getSyncedPapers(syncLogId: number): Promise<SyncedPaper[]> {
  const res = await apiGet<{ papers: SyncedPaper[] }>(`/admin/sync-logs/${syncLogId}/papers`);
  return res.papers ?? [];
}

// ---- Run weekly sync (POST /admin/run-weekly-now) ----
export interface RunSyncResult {
  added: number;
  alreadyExists: number;
  errors: number;
  reprocessStarted: boolean;
}
/** POST /admin/run-weekly-now — chạy NGAY sync OpenAlex thật (fetch bài mới + đào keyword nền). */
export async function runWeeklySync(): Promise<RunSyncResult> {
  return apiPost<RunSyncResult>('/admin/run-weekly-now');
}

// ---- Papers (GET /admin/papers) ----
interface BePaper { paperId: string; title: string; publicationYear: number; citationCount: number; journalName: string; }
export async function getPapers(): Promise<RepositoryPaper[]> {
  const res = await apiGet<Paged<BePaper>>('/admin/papers', { page: 1, pageSize: 50 });
  return res.items.map((p) => ({
    id: p.paperId,
    title: p.title,
    doi: '',
    journal: p.journalName,
    year: p.publicationYear,
    citations: p.citationCount,
    status: 'ACTIVE',
  }));
}

// ---- Subscription plans (GET /admin/subscriptions/plans) ----
interface BePlan { planId: number; planName: string; priceAmount: number; durationDays: number; isActive: boolean; }
export async function getPlans(): Promise<SubscriptionPlan[]> {
  const res = await apiGet<BePlan[]>('/admin/subscriptions/plans');
  return res.map((p) => ({
    id: String(p.planId),
    name: p.planName,
    price: `${p.priceAmount.toLocaleString('vi-VN')}đ`,
    duration: `${p.durationDays} ngày`,
    status: p.isActive ? 'ACTIVE' : 'EXPIRED',
    priceAmount: p.priceAmount,
    durationDays: p.durationDays,
  }));
}

// ---- Plan mutations (admin) ----
export interface PlanInput { planName: string; priceAmount: number; durationDays: number; isActive: boolean; }

/** PUT /admin/subscriptions/plans/{id} — cập nhật tên/giá/thời hạn/trạng thái gói. */
export function updatePlan(planId: string, input: PlanInput): Promise<unknown> {
  return apiPut(`/admin/subscriptions/plans/${planId}`, input);
}

/** POST /admin/subscriptions/plans — tạo gói cước mới. */
export function createPlan(input: Omit<PlanInput, 'isActive'>): Promise<unknown> {
  return apiPost('/admin/subscriptions/plans', input);
}

/** PATCH /admin/subscriptions/plans/{id}/toggle?isActive= — bật/tắt gói. */
export function togglePlan(planId: string, isActive: boolean): Promise<unknown> {
  return apiPatch(`/admin/subscriptions/plans/${planId}/toggle?isActive=${isActive}`);
}

/** DELETE /admin/subscriptions/plans/{id} — xoá cứng gói (chỉ khi chưa có người đăng ký). */
export function deletePlan(planId: string): Promise<unknown> {
  return apiDelete(`/admin/subscriptions/plans/${planId}`);
}

// ---- Transactions (GET /admin/transactions) ----
interface BeTransaction { subscriptionId: number; customerName: string; customerEmail: string; planName: string; amount: number; status: string; createdAt: string; }
export async function getTransactions(): Promise<RevenueRow[]> {
  const res = await apiGet<BeTransaction[]>('/admin/transactions');
  return res.map((t) => ({
    invoiceId: `#SUB-${t.subscriptionId}`,
    customer: t.customerName || t.customerEmail,
    plan: t.planName,
    amount: `${t.amount.toLocaleString('vi-VN')}đ`,
    method: 'PayOS / VietQR',
    paidAt: toVnTime(t.createdAt),
    status: t.status === 'ACTIVE' ? 'SUCCESS' : (t.status as AdminStatus),
  }));
}

// ---- Revenue chart bars (GET /admin/dashboard/charts) ----
interface BeCharts { monthlyRevenues: { month: string; revenue: number }[]; }
export async function getRevenueBars(): Promise<{ month: string; amount: number }[]> {
  const res = await apiGet<BeCharts>('/admin/dashboard/charts');
  return (res.monthlyRevenues ?? []).map((m) => ({ month: m.month, amount: m.revenue }));
}

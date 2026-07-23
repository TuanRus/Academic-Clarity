import { useEffect, useState } from 'react';
import AdminBadge from '../../components/admin/AdminBadge';
import AdminMetricCard from '../../components/admin/AdminMetricCard';
import AdminModal from '../../components/admin/AdminModal';
import AdminSectionCard from '../../components/admin/AdminSectionCard';
import AdminTable from '../../components/admin/AdminTable';
import AdminToast from '../../components/admin/AdminToast';
import {
  getTransactions,
  getPlans,
  getSystemConfig,
  updatePlan,
  createPlan,
  togglePlan,
  deletePlan,
  type RevenueRow,
  type SubscriptionPlan,
} from '../../lib/api/admin';
import { ApiError } from '../../lib/http';

const AdminPaymentPage = () => {
  const [rows, setRows] = useState<RevenueRow[]>([]);
  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  const [payosConfigured, setPayosConfigured] = useState<boolean | null>(null);
  useEffect(() => {
    getTransactions().then(setRows).catch(() => setRows([]));
    getPlans().then(setPlans).catch(() => setPlans([]));
    // Trạng thái cấu hình cổng thanh toán PayOS (chuyển từ Settings sang đây).
    getSystemConfig()
      .then((c) => setPayosConfigured(c.integrations.find((i) => i.name === 'PayOS')?.configured ?? false))
      .catch(() => setPayosConfigured(null));
  }, []);
  const [statusFilter, setStatusFilter] = useState<'ALL' | RevenueRow['status']>('ALL');
  const [selectedRow, setSelectedRow] = useState<RevenueRow | null>(null);
  const [editingPlan, setEditingPlan] = useState<SubscriptionPlan | null>(null);
  const [planName, setPlanName] = useState('');
  const [planPrice, setPlanPrice] = useState('');
  const [planDuration, setPlanDuration] = useState('');
  const [planIsActive, setPlanIsActive] = useState(true);
  const [toast, setToast] = useState<string | null>(null);

  const filteredRows =
    statusFilter === 'ALL' ? rows : rows.filter((row) => row.status === statusFilter);

  const totalSubscriptions = rows.length;
  // Số USER riêng biệt đã mua gói thành công (distinct theo email, không đếm trùng khi 1 người mua nhiều lần).
  const payingUsers = new Set(
    rows.filter((row) => row.status === 'SUCCESS').map((row) => row.customerEmail),
  ).size;

  // Subscription Distribution (chuyển từ Dashboard sang đây) — gom theo tên gói từ giao dịch thật.
  const planColors = ['#4338ca', '#10b981', '#fb923c', '#0ea5e9', '#e11d48'];
  const planDistribution = (() => {
    const map = new Map<string, number>();
    rows.forEach((r) => map.set(r.plan, (map.get(r.plan) ?? 0) + 1));
    const total = rows.length;
    return [...map.entries()].map(([name, count], i) => ({
      name,
      count,
      percent: total > 0 ? Math.round((count / total) * 100) : 0,
      color: planColors[i % planColors.length],
    }));
  })();
  // Đếm số lượng gói theo chu kỳ Tháng / Năm (dựa trên tên gói đã chuẩn hoá "… Monthly/Yearly").
  const monthlyCount = rows.filter((r) => /monthly|tháng/i.test(r.plan)).length;
  const yearlyCount = rows.filter((r) => /yearly|năm/i.test(r.plan)).length;

  const exportFinanceReport = () => {
    const content = rows
      .map(
        (row) =>
          `${row.invoiceId},${row.transactionId},${row.customer},${row.plan},${row.amount},${row.method},${row.paidAt},${row.status}`,
      )
      .join('\n');

    const blob = new Blob(
      [`Invoice,Transaction ID,Customer,Plan,Amount,Method,Paid At,Status\n${content}`],
      { type: 'text/csv;charset=utf-8;' },
    );

    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');

    anchor.href = url;
    anchor.download = 'finance-report.csv';
    anchor.click();

    URL.revokeObjectURL(url);
    setToast('Finance report exported.');
  };

  const refreshPayments = async () => {
    try {
      setRows(await getTransactions());
      setToast('Payments refreshed.');
    } catch (e) {
      setToast(e instanceof ApiError ? e.message : 'Failed to refresh payments.');
    }
  };

  const openEditPlan = (plan: SubscriptionPlan) => {
    setEditingPlan(plan);
    setPlanName(plan.name);
    setPlanPrice(String(plan.priceAmount));
    setPlanDuration(String(plan.durationDays));
    setPlanIsActive(plan.status === 'ACTIVE');
  };

  const [savingPlan, setSavingPlan] = useState(false);
  const savePlan = async () => {
    if (!editingPlan) return;
    const amount = Number(planPrice);
    const days = Number(planDuration);

    if (!planName.trim()) { setToast('Plan name is required.'); return; }
    if (!Number.isFinite(amount) || amount < 0) { setToast('Enter a valid price (number in VND).'); return; }
    if (!Number.isInteger(days) || days <= 0) { setToast('Enter valid duration days (positive integer).'); return; }

    setSavingPlan(true);
    try {
      await updatePlan(editingPlan.id, {
        planName: planName.trim(),
        priceAmount: amount,
        durationDays: days,
        isActive: planIsActive,
      });
      setPlans(await getPlans());
      setEditingPlan(null);
      setToast(`Plan "${planName.trim()}" updated successfully.`);
    } catch (e) {
      setToast(e instanceof ApiError ? e.message : 'Failed to update plan.');
    } finally {
      setSavingPlan(false);
    }
  };

  const handleTogglePlan = async (plan: SubscriptionPlan) => {
    const nextStatus = plan.status !== 'ACTIVE';
    try {
      await togglePlan(plan.id, nextStatus);
      setPlans(await getPlans());
      setToast(`Plan "${plan.name}" status changed to ${nextStatus ? 'Active' : 'Inactive'}.`);
    } catch (e) {
      setToast(e instanceof ApiError ? e.message : 'Failed to change plan status.');
    }
  };

  // States cho modal Tạo gói mới
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [createName, setCreateName] = useState('');
  const [createPrice, setCreatePrice] = useState('');
  const [createDuration, setCreateDuration] = useState('30');
  const [creatingPlan, setCreatingPlan] = useState(false);

  const handleCreatePlan = async () => {
    const amount = Number(createPrice);
    const days = Number(createDuration);

    if (!createName.trim()) { setToast('Plan name is required.'); return; }
    if (!Number.isFinite(amount) || amount < 0) { setToast('Enter a valid price in VND.'); return; }
    if (!Number.isInteger(days) || days <= 0) { setToast('Enter valid duration days.'); return; }

    setCreatingPlan(true);
    try {
      await createPlan({
        planName: createName.trim(),
        priceAmount: amount,
        durationDays: days,
      });
      setPlans(await getPlans());
      setShowCreateModal(false);
      setCreateName(''); setCreatePrice(''); setCreateDuration('30');
      setToast(`New plan "${createName.trim()}" created successfully.`);
    } catch (e) {
      setToast(e instanceof ApiError ? e.message : 'Failed to create plan.');
    } finally {
      setCreatingPlan(false);
    }
  };

  const handleDeletePlan = async (plan: SubscriptionPlan) => {
    if (!window.confirm(`Are you sure you want to delete plan "${plan.name}"?`)) return;
    try {
      await deletePlan(plan.id);
      setPlans(await getPlans());
      setToast(`Plan "${plan.name}" deleted successfully.`);
    } catch (e) {
      setToast(e instanceof ApiError ? e.message : 'Cannot delete plan — it may have active subscribers.');
    }
  };

  return (
    <div className="space-y-5">
      <AdminToast message={toast} onClose={() => setToast(null)} />

      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight text-slate-950">
            Revenue & Subscription Dashboard
          </h1>
          <p className="mt-1 text-xs text-slate-500">
            Monitor subscription revenue, payment transactions and premium plan performance.
          </p>
          {payosConfigured !== null && (
            <span
              className={`mt-2 inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-[11px] font-semibold ${
                payosConfigured ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500'
              }`}
            >
              {payosConfigured ? '✓' : '✗'} PayOS gateway {payosConfigured ? 'configured' : 'not configured'}
            </span>
          )}
        </div>

        <div className="flex gap-3">
          <button
            onClick={exportFinanceReport}
            className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700 hover:bg-slate-50"
          >
            ⇩ Export Finance Report
          </button>

          <button
            onClick={refreshPayments}
            className="rounded-md bg-[#4338ca] px-4 py-2 text-xs font-bold text-white hover:bg-[#3730a3]"
          >
            Refresh Payments
          </button>
        </div>
      </div>

      <div className="grid gap-5 xl:grid-cols-2">
        <AdminMetricCard
          label="Total Subscriptions"
          value={String(totalSubscriptions)}
          helper="All subscription transactions"
          icon="★"
          accent="orange"
        />

        <AdminMetricCard
          label="Paying Users"
          value={String(payingUsers)}
          helper="Distinct users who purchased a plan"
          icon="✓"
          accent="green"
        />
      </div>

      <AdminSectionCard title="Subscription Distribution" subtitle="Plan allocation and billing cycle breakdown from real transactions">
        <div className="grid gap-6 p-6 lg:grid-cols-[1fr_260px]">
          <div className="space-y-5">
            {planDistribution.length === 0 ? (
              <p className="text-sm text-slate-500">No subscription data available.</p>
            ) : (
              planDistribution.map((plan) => (
                <div key={plan.name}>
                  <div className="mb-2 flex justify-between text-sm font-semibold">
                    <span>{plan.name}</span>
                    <span>{plan.count} · {plan.percent}%</span>
                  </div>
                  <div className="h-3 rounded-full bg-slate-100">
                    <div className="h-3 rounded-full" style={{ width: `${plan.percent}%`, backgroundColor: plan.color }} />
                  </div>
                </div>
              ))
            )}
          </div>

          <div className="space-y-3">
            <p className="text-xs font-bold uppercase tracking-wide text-slate-400">By billing cycle</p>
            <div className="flex items-center justify-between rounded-xl bg-slate-50 px-4 py-3">
              <span className="flex items-center gap-2 text-sm font-semibold text-slate-700">
                <span className="h-3 w-3 rounded-full bg-[#4338ca]" /> Monthly plans
              </span>
              <span className="text-lg font-extrabold text-slate-950">{monthlyCount}</span>
            </div>
            <div className="flex items-center justify-between rounded-xl bg-slate-50 px-4 py-3">
              <span className="flex items-center gap-2 text-sm font-semibold text-slate-700">
                <span className="h-3 w-3 rounded-full bg-emerald-500" /> Yearly plans
              </span>
              <span className="text-lg font-extrabold text-slate-950">{yearlyCount}</span>
            </div>
          </div>
        </div>
      </AdminSectionCard>

      <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_340px]">
        <AdminSectionCard
          title="Recent Payment Transactions"
          subtitle="Subscription plan purchases and gateway callbacks"
          action={
            <select
              value={statusFilter}
              onChange={(event) =>
                setStatusFilter(event.target.value as 'ALL' | RevenueRow['status'])
              }
              className="rounded-md border border-slate-200 px-3 py-2 text-xs font-bold text-slate-600"
            >
              <option value="ALL">All Status</option>
              <option value="SUCCESS">Success</option>
              <option value="PENDING">Pending</option>
              <option value="FAILED">Failed</option>
            </select>
          }
        >
          <AdminTable
            headers={[
              'Invoice ID',
              'Transaction ID',
              'Customer',
              'Plan',
              'Amount',
              'Method',
              'Paid At',
              'Status',
              'Actions',
            ]}
          >
            {filteredRows.map((row) => (
              <tr key={row.invoiceId} className="hover:bg-slate-50">
                <td className="px-5 py-4 font-bold text-slate-700">{row.invoiceId}</td>
                <td className="px-5 py-4 font-semibold text-slate-700">
                  {row.transactionId}
                </td>
                <td className="px-5 py-4">{row.customer}</td>
                <td className="px-5 py-4 font-semibold text-slate-800">{row.plan}</td>
                <td className="px-5 py-4 font-bold text-slate-950">{row.amount}</td>
                <td className="px-5 py-4">{row.method}</td>
                <td className="px-5 py-4">{row.paidAt}</td>
                <td className="px-5 py-4">
                  <AdminBadge status={row.status} />
                </td>
                <td className="px-5 py-4">
                  <button
                    onClick={() => setSelectedRow(row)}
                    className="text-xs font-bold text-[#0b6fb8] hover:underline"
                  >
                    Detail
                  </button>
                </td>
              </tr>
            ))}
          </AdminTable>
        </AdminSectionCard>

        <AdminSectionCard
          title="Plan Management"
          subtitle="Manage subscription plans and pricing"
          action={
            <button
              onClick={() => setShowCreateModal(true)}
              className="rounded-md bg-[#4338ca] px-3 py-1.5 text-xs font-bold text-white hover:bg-[#3730a3]"
            >
              + Create Plan
            </button>
          }
        >
          <div className="space-y-3 p-5">
            {plans.map((plan) => (
              <div key={plan.id} className="rounded-md border border-slate-200 p-3">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-sm font-extrabold text-slate-900">{plan.name}</p>
                    <p className="mt-1 text-xs text-slate-500">
                      {plan.price} · {plan.duration}
                    </p>
                  </div>

                  <AdminBadge status={plan.status} />
                </div>

                <div className="mt-3 flex items-center gap-2">
                  <button
                    onClick={() => openEditPlan(plan)}
                    className="rounded border border-slate-300 px-2.5 py-1 text-xs font-bold text-slate-700 hover:bg-slate-50"
                  >
                    Edit Plan
                  </button>

                  <button
                    onClick={() => handleTogglePlan(plan)}
                    className={`rounded border px-2.5 py-1 text-xs font-bold ${
                      plan.status === 'ACTIVE'
                        ? 'border-amber-300 text-amber-700 hover:bg-amber-50'
                        : 'border-emerald-300 text-emerald-700 hover:bg-emerald-50'
                    }`}
                  >
                    {plan.status === 'ACTIVE' ? 'Disable' : 'Enable'}
                  </button>

                  <button
                    onClick={() => handleDeletePlan(plan)}
                    className="rounded border border-rose-200 px-2.5 py-1 text-xs font-bold text-rose-600 hover:bg-rose-50"
                  >
                    Delete
                  </button>
                </div>
              </div>
            ))}

            <div className="rounded-md bg-emerald-50 p-4 text-sm text-emerald-800">
              Edu accounts are receiving a configured 50% discount for premium checkout.
            </div>
          </div>
        </AdminSectionCard>
      </div>

      <AdminModal
        open={Boolean(selectedRow)}
        title="Transaction Detail"
        subtitle="Gateway callback and subscription payment metadata."
        onClose={() => setSelectedRow(null)}
      >
        {selectedRow && (
          <div className="grid gap-3 text-sm text-slate-700 sm:grid-cols-2">
            <p>
              <span className="font-bold">Invoice:</span> {selectedRow.invoiceId}
            </p>
            <p>
              <span className="font-bold">Transaction ID:</span>{' '}
              {selectedRow.transactionId}
            </p>
            <p>
              <span className="font-bold">Customer:</span> {selectedRow.customer}
            </p>
            <p>
              <span className="font-bold">Plan:</span> {selectedRow.plan}
            </p>
            <p>
              <span className="font-bold">Amount:</span> {selectedRow.amount}
            </p>
            <p>
              <span className="font-bold">Method:</span> {selectedRow.method}
            </p>
            <p>
              <span className="font-bold">Paid At:</span> {selectedRow.paidAt}
            </p>
            <p className="sm:col-span-2">
              <span className="font-bold">Status:</span>{' '}
              <AdminBadge status={selectedRow.status} />
            </p>
          </div>
        )}
      </AdminModal>

      <AdminModal
        open={Boolean(editingPlan)}
        title="Edit Subscription Plan"
        subtitle="Update plan name, pricing, duration, and active status."
        onClose={() => setEditingPlan(null)}
        footer={
          <>
            <button
              onClick={() => setEditingPlan(null)}
              className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700"
            >
              Cancel
            </button>

            <button
              onClick={savePlan}
              disabled={savingPlan}
              className="rounded-md bg-[#4338ca] px-4 py-2 text-xs font-bold text-white disabled:opacity-50"
            >
              {savingPlan ? 'Saving…' : 'Save Changes'}
            </button>
          </>
        }
      >
        {editingPlan && (
          <div className="space-y-3">
            <label className="block">
              <span className="mb-1 block text-xs font-semibold text-slate-600">Plan Name</span>
              <input
                value={planName}
                onChange={(event) => setPlanName(event.target.value)}
                placeholder="e.g. Premium Monthly"
                className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]"
              />
            </label>

            <div className="grid grid-cols-2 gap-3">
              <label className="block">
                <span className="mb-1 block text-xs font-semibold text-slate-600">Price (VND)</span>
                <input
                  value={planPrice}
                  onChange={(event) => setPlanPrice(event.target.value)}
                  inputMode="numeric"
                  placeholder="e.g. 50000"
                  className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]"
                />
              </label>

              <label className="block">
                <span className="mb-1 block text-xs font-semibold text-slate-600">Duration (Days)</span>
                <input
                  value={planDuration}
                  onChange={(event) => setPlanDuration(event.target.value)}
                  inputMode="numeric"
                  placeholder="e.g. 30"
                  className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]"
                />
              </label>
            </div>

            <label className="flex items-center gap-2 pt-2">
              <input
                type="checkbox"
                checked={planIsActive}
                onChange={(e) => setPlanIsActive(e.target.checked)}
                className="h-4 w-4 rounded border-slate-300 text-[#4338ca]"
              />
              <span className="text-xs font-bold text-slate-700">Active (Visible for checkout)</span>
            </label>
          </div>
        )}
      </AdminModal>

      {/* Modal Tạo gói cước mới */}
      <AdminModal
        open={showCreateModal}
        title="Create New Subscription Plan"
        subtitle="Add a new subscription plan for premium users."
        onClose={() => setShowCreateModal(false)}
        footer={
          <>
            <button
              onClick={() => setShowCreateModal(false)}
              className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700"
            >
              Cancel
            </button>

            <button
              onClick={handleCreatePlan}
              disabled={creatingPlan}
              className="rounded-md bg-[#4338ca] px-4 py-2 text-xs font-bold text-white disabled:opacity-50"
            >
              {creatingPlan ? 'Creating…' : 'Create Plan'}
            </button>
          </>
        }
      >
        <div className="space-y-3">
          <label className="block">
            <span className="mb-1 block text-xs font-semibold text-slate-600">Plan Name</span>
            <input
              value={createName}
              onChange={(e) => setCreateName(e.target.value)}
              placeholder="e.g. Academic Premium Yearly"
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]"
            />
          </label>

          <div className="grid grid-cols-2 gap-3">
            <label className="block">
              <span className="mb-1 block text-xs font-semibold text-slate-600">Price (VND)</span>
              <input
                value={createPrice}
                onChange={(e) => setCreatePrice(e.target.value)}
                inputMode="numeric"
                placeholder="e.g. 500000"
                className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]"
              />
            </label>

            <label className="block">
              <span className="mb-1 block text-xs font-semibold text-slate-600">Duration (Days)</span>
              <input
                value={createDuration}
                onChange={(e) => setCreateDuration(e.target.value)}
                inputMode="numeric"
                placeholder="e.g. 365"
                className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]"
              />
            </label>
          </div>
        </div>
      </AdminModal>
    </div>
  );
};

export default AdminPaymentPage;
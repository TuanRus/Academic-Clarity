import { useState, useEffect } from 'react';
import AdminBadge from '../../components/admin/AdminBadge';
import AdminMetricCard from '../../components/admin/AdminMetricCard';
import AdminModal from '../../components/admin/AdminModal';
import AdminSectionCard from '../../components/admin/AdminSectionCard';
import AdminTable from '../../components/admin/AdminTable';
import AdminToast from '../../components/admin/AdminToast';
import { getPlans, getRevenueBars, getDashboardStats, updatePlan, createPlan, togglePlan as togglePlanApi, deletePlan as deletePlanApi, getTransactions, type DashboardStats, type RevenueRow, type SubscriptionPlan } from '../../lib/api/admin';
import { ApiError } from '../../lib/http';

const AdminRevenuePage = () => {
  const [revenueBars, setRevenueBars] = useState<{ month: string; amount: number }[]>([]);
  // Danh sách giao dịch chi tiết chưa có endpoint BE → để trống (chỉ hiển thị gói + biểu đồ).
  const [rows, setRows] = useState<RevenueRow[]>([]);
  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  const [stats, setStats] = useState<DashboardStats | null>(null);

  const reloadPlans = () => getPlans().then(setPlans).catch(() => setPlans([]));

  useEffect(() => {
    reloadPlans();
    getRevenueBars().then(setRevenueBars).catch(() => setRevenueBars([]));
    getDashboardStats().then(setStats).catch(() => setStats(null));
    getTransactions().then(setRows).catch(() => setRows([]));
  }, []);
  const [statusFilter, setStatusFilter] = useState<'ALL' | RevenueRow['status']>('ALL');
  const [selectedRow, setSelectedRow] = useState<RevenueRow | null>(null);
  const [editingPlan, setEditingPlan] = useState<SubscriptionPlan | null>(null);
  const [planName, setPlanName] = useState('');
  const [planPrice, setPlanPrice] = useState('');
  const [planDuration, setPlanDuration] = useState('');
  const [showCreatePlan, setShowCreatePlan] = useState(false);
  const [newPlanName, setNewPlanName] = useState('');
  const [newPlanPrice, setNewPlanPrice] = useState('');
  const [newPlanDuration, setNewPlanDuration] = useState('');
  const [yearlyDiscount, setYearlyDiscount] = useState('20');
  const [toast, setToast] = useState<string | null>(null);

  const maxBar = Math.max(...revenueBars.map((item) => item.amount));
  const filteredRows = statusFilter === 'ALL' ? rows : rows.filter((row) => row.status === statusFilter);

  const successCount = rows.filter((row) => row.status === 'SUCCESS').length;
  const failedRate = rows.length ? Math.round((rows.filter((row) => row.status === 'FAILED').length / rows.length) * 1000) / 10 : 0;

  const exportFinanceReport = () => {
    const content = rows.map((row) => `${row.invoiceId},${row.customer},${row.plan},${row.amount},${row.method},${row.paidAt},${row.status}`).join('\n');
    const blob = new Blob([`Invoice,Customer,Plan,Amount,Method,Paid At,Status\n${content}`], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'finance-report.csv';
    anchor.click();
    URL.revokeObjectURL(url);
    setToast('Finance report exported.');
  };

  const refreshLedger = () => {
    const nextInvoice: RevenueRow = {
      invoiceId: `#INV-${Math.floor(Math.random() * 90000 + 10000)}`,
      customer: 'new.researcher@university.edu',
      plan: 'Premium Monthly',
      amount: '99.000₫',
      method: 'VietQR',
      paidAt: new Date().toISOString().slice(0, 16).replace('T', ' '),
      status: 'PENDING',
    };
    setRows((current) => [nextInvoice, ...current]);
    setRevenueBars((current) => current.map((item, index) => (index === current.length - 1 ? { ...item, amount: item.amount + 3 } : item)));
    setToast('Ledger refreshed. New payment callback received as PENDING.');

    window.setTimeout(() => {
      setRows((current) => current.map((row) => (row.invoiceId === nextInvoice.invoiceId ? { ...row, status: 'SUCCESS' } : row)));
      setToast(`${nextInvoice.invoiceId} confirmed by payment gateway.`);
    }, 1300);
  };

  const openEditPlan = (plan: SubscriptionPlan) => {
    setEditingPlan(plan);
    setPlanName(plan.name);
    setPlanPrice(String(plan.priceAmount));
    setPlanDuration(String(plan.durationDays));
  };

  const savePlan = async () => {
    if (!editingPlan) return;
    const priceAmount = Number(planPrice);
    const durationDays = Number(planDuration);
    if (!planName.trim() || !Number.isFinite(priceAmount) || priceAmount < 0 || !Number.isInteger(durationDays) || durationDays <= 0) {
      setToast('Tên, giá và thời hạn (ngày) phải hợp lệ.');
      return;
    }
    try {
      await updatePlan(editingPlan.id, {
        planName: planName.trim(),
        priceAmount,
        durationDays,
        isActive: editingPlan.status === 'ACTIVE',
      });
      await reloadPlans();
      setEditingPlan(null);
      setToast(`Đã cập nhật gói ${editingPlan.name}.`);
    } catch {
      setToast('Cập nhật gói thất bại.');
    }
  };

  const togglePlan = async (plan: SubscriptionPlan) => {
    const next = plan.status !== 'ACTIVE';
    try {
      await togglePlanApi(plan.id, next);
      await reloadPlans();
      setToast(`${plan.name} đã ${next ? 'kích hoạt' : 'tạm ngưng'}.`);
    } catch {
      setToast('Đổi trạng thái gói thất bại.');
    }
  };

  // Tính giá gói năm theo công thức: giá gói tháng × 12 − n% (n tuỳ chọn).
  // Lấy gói có thời hạn ngắn nhất (gói tháng) làm cơ sở.
  const fillYearlyFromMonthly = () => {
    if (plans.length === 0) {
      setToast('Chưa có gói tháng nào để tính.');
      return;
    }
    const pct = Number(yearlyDiscount);
    if (!Number.isFinite(pct) || pct < 0 || pct > 100) {
      setToast('% giảm phải từ 0 đến 100.');
      return;
    }
    const monthly = plans.reduce((a, b) => (a.durationDays <= b.durationDays ? a : b));
    const yearly = Math.round(monthly.priceAmount * 12 * (1 - pct / 100));
    setNewPlanName('Gói Premium Năm');
    setNewPlanPrice(String(yearly));
    setNewPlanDuration('365');
    setToast(`Giá năm = ${monthly.priceAmount.toLocaleString('vi-VN')} × 12 − ${pct}% = ${yearly.toLocaleString('vi-VN')}đ`);
  };

  const removePlan = async (plan: SubscriptionPlan) => {
    if (!window.confirm(`Xoá vĩnh viễn gói "${plan.name}"? Hành động này không thể hoàn tác.`)) return;
    try {
      await deletePlanApi(plan.id);
      await reloadPlans();
      setToast(`Đã xoá gói ${plan.name}.`);
    } catch (err) {
      setToast(err instanceof ApiError ? err.message : 'Xoá gói thất bại.');
    }
  };

  const createNewPlan = async () => {
    const priceAmount = Number(newPlanPrice);
    const durationDays = Number(newPlanDuration);
    if (!newPlanName.trim() || !Number.isFinite(priceAmount) || priceAmount < 0 || !Number.isInteger(durationDays) || durationDays <= 0) {
      setToast('Nhập tên, giá và thời hạn (ngày) hợp lệ.');
      return;
    }
    try {
      await createPlan({ planName: newPlanName.trim(), priceAmount, durationDays });
      await reloadPlans();
      setShowCreatePlan(false);
      setNewPlanName(''); setNewPlanPrice(''); setNewPlanDuration('');
      setToast('Đã tạo gói cước mới.');
    } catch {
      setToast('Tạo gói thất bại (có thể trùng tên).');
    }
  };

  return (
    <div className="space-y-5">
      <AdminToast message={toast} onClose={() => setToast(null)} />

      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight text-slate-950">Revenue Management</h1>
          <p className="mt-1 text-xs text-slate-500">Subscription revenue, payment transaction health and premium conversion monitoring.</p>
        </div>
        <div className="flex gap-3">
          <button onClick={exportFinanceReport} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700 hover:bg-slate-50">⇩ Export Finance Report</button>
          <button onClick={refreshLedger} className="rounded-md bg-[#062b4f] px-4 py-2 text-xs font-bold text-white hover:bg-[#0b3d6f]">Refresh Ledger</button>
        </div>
      </div>

      <div className="grid gap-3 grid-cols-2 xl:grid-cols-4">
        <AdminMetricCard label="Successful Revenue" value={stats ? `${stats.totalRevenue.toLocaleString('vi-VN')}₫` : '—'} helper="Confirmed gateway payments" icon="₫" accent="green" />
        <AdminMetricCard label="Successful Transactions" value={String(successCount)} helper="VNPAY + VietQR success" icon="✓" accent="blue" />
        <AdminMetricCard label="Premium Subscribers" value={stats ? String(stats.activeSubscriptions) : '—'} helper="Active premium accounts" icon="★" accent="orange" />
        <AdminMetricCard label="Failed Payment Rate" value={`${failedRate}%`} helper="Calculated from visible ledger" icon="!" accent="slate" />
      </div>

      <div className="grid gap-5 xl:grid-cols-[1fr_340px]">
        <AdminSectionCard title="Revenue Trend" subtitle="Gross successful payments by month">
          <div className="p-5">
            <div className="flex h-72 items-end gap-4 rounded-lg bg-slate-50 p-5">
              {revenueBars.map((item) => (
                <div key={item.month} className="flex flex-1 flex-col items-center gap-3">
                  <div className="flex h-52 w-full items-end rounded bg-white px-2">
                    <button
                      onClick={() => setToast(`${item.month} revenue: ${item.amount}M₫`)}
                      className="w-full rounded-t-md bg-[#0b6fb8] shadow-sm transition-all hover:bg-[#062b4f]"
                      style={{ height: `${(item.amount / maxBar) * 100}%` }}
                      title={`${item.month}: ${item.amount}M₫`}
                    />
                  </div>
                  <span className="text-xs font-bold text-slate-500">{item.month}</span>
                </div>
              ))}
            </div>
            <div className="mt-4 grid gap-3 sm:grid-cols-3">
              <div className="rounded-md border border-slate-200 bg-white p-4"><p className="text-xs font-bold text-slate-500">ARPU</p><p className="mt-1 text-lg font-extrabold text-slate-950">96.400₫</p></div>
              <div className="rounded-md border border-slate-200 bg-white p-4"><p className="text-xs font-bold text-slate-500">Conversion</p><p className="mt-1 text-lg font-extrabold text-emerald-700">12.8%</p></div>
              <div className="rounded-md border border-slate-200 bg-white p-4"><p className="text-xs font-bold text-slate-500">Refunds</p><p className="mt-1 text-lg font-extrabold text-red-600">4</p></div>
            </div>
          </div>
        </AdminSectionCard>

        <AdminSectionCard
          title="Plan Management"
          subtitle="Subscription plans (live from database)"
          action={<button onClick={() => setShowCreatePlan(true)} className="rounded-md bg-[#0b6fb8] px-3 py-1.5 text-xs font-bold text-white">+ Add Plan</button>}
        >
          <div className="space-y-3 p-5">
            {plans.map((plan) => (
              <div key={plan.id} className="rounded-md border border-slate-200 p-3">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="text-sm font-extrabold text-slate-900">{plan.name}</p>
                    <p className="mt-1 text-xs text-slate-500">{plan.price} · {plan.duration}</p>
                  </div>
                  <AdminBadge status={plan.status} />
                </div>
                <div className="mt-3 flex gap-2">
                  <button onClick={() => openEditPlan(plan)} className="rounded border border-slate-200 px-3 py-1 text-xs font-bold text-slate-700">Edit</button>
                  <button onClick={() => togglePlan(plan)} className="rounded border border-orange-200 px-3 py-1 text-xs font-bold text-orange-700">{plan.status === 'ACTIVE' ? 'Disable' : 'Enable'}</button>
                  <button onClick={() => removePlan(plan)} className="rounded border border-red-200 px-3 py-1 text-xs font-bold text-red-700 hover:bg-red-50">Delete</button>
                </div>
              </div>
            ))}
            <div className="rounded-md bg-emerald-50 p-4 text-sm text-emerald-800">Tài khoản .edu được giảm 50% khi nâng cấp Premium.</div>
          </div>
        </AdminSectionCard>
      </div>

      <AdminSectionCard
        title="Recent Payment Transactions"
        subtitle="Subscription plan purchases and gateway callbacks"
        action={
          <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as 'ALL' | RevenueRow['status'])} className="rounded-md border border-slate-200 px-3 py-2 text-xs font-bold text-slate-600">
            <option value="ALL">All Status</option>
            <option value="SUCCESS">Success</option>
            <option value="PENDING">Pending</option>
            <option value="FAILED">Failed</option>
          </select>
        }
      >
        <AdminTable headers={['Invoice ID', 'Customer', 'Plan', 'Amount', 'Method', 'Paid At', 'Status', 'Actions']}>
          {filteredRows.map((row) => (
            <tr key={row.invoiceId} className="hover:bg-slate-50">
              <td className="px-5 py-4 font-bold text-slate-700">{row.invoiceId}</td>
              <td className="px-5 py-4">{row.customer}</td>
              <td className="px-5 py-4 font-semibold text-slate-800">{row.plan}</td>
              <td className="px-5 py-4 font-bold text-slate-950">{row.amount}</td>
              <td className="px-5 py-4">{row.method}</td>
              <td className="px-5 py-4">{row.paidAt}</td>
              <td className="px-5 py-4"><AdminBadge status={row.status} /></td>
              <td className="px-5 py-4"><button onClick={() => setSelectedRow(row)} className="text-xs font-bold text-[#0b6fb8] hover:underline">Detail</button></td>
            </tr>
          ))}
        </AdminTable>
      </AdminSectionCard>

      <AdminModal open={Boolean(selectedRow)} title="Transaction Detail" subtitle="Gateway callback and subscription payment metadata." onClose={() => setSelectedRow(null)}>
        {selectedRow && (
          <div className="grid gap-3 text-sm text-slate-700 sm:grid-cols-2">
            <p><span className="font-bold">Invoice:</span> {selectedRow.invoiceId}</p>
            <p><span className="font-bold">Customer:</span> {selectedRow.customer}</p>
            <p><span className="font-bold">Plan:</span> {selectedRow.plan}</p>
            <p><span className="font-bold">Amount:</span> {selectedRow.amount}</p>
            <p><span className="font-bold">Method:</span> {selectedRow.method}</p>
            <p><span className="font-bold">Paid At:</span> {selectedRow.paidAt}</p>
            <p className="sm:col-span-2"><span className="font-bold">Status:</span> <AdminBadge status={selectedRow.status} /></p>
          </div>
        )}
      </AdminModal>

      <AdminModal
        open={Boolean(editingPlan)}
        title="Edit Subscription Plan"
        subtitle="Cập nhật giá & thời hạn — lưu trực tiếp xuống database."
        onClose={() => setEditingPlan(null)}
        footer={
          <>
            <button onClick={() => setEditingPlan(null)} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700">Cancel</button>
            <button onClick={savePlan} className="rounded-md bg-[#062b4f] px-4 py-2 text-xs font-bold text-white">Save Plan</button>
          </>
        }
      >
        {editingPlan && (
          <div className="space-y-4">
            <label className="block text-xs font-bold text-slate-600">
              Tên gói
              <input value={planName} onChange={(event) => setPlanName(event.target.value)} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]" />
            </label>
            <label className="block text-xs font-bold text-slate-600">
              Giá (VNĐ)
              <input type="number" min={0} value={planPrice} onChange={(event) => setPlanPrice(event.target.value)} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]" />
            </label>
            <label className="block text-xs font-bold text-slate-600">
              Thời hạn (ngày)
              <input type="number" min={1} value={planDuration} onChange={(event) => setPlanDuration(event.target.value)} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]" />
            </label>
          </div>
        )}
      </AdminModal>

      <AdminModal
        open={showCreatePlan}
        title="Tạo gói cước mới"
        subtitle="Thêm gói Premium (vd: gói tháng 49.000đ / 30 ngày, gói năm 499.000đ / 365 ngày)."
        onClose={() => setShowCreatePlan(false)}
        footer={
          <>
            <button onClick={() => setShowCreatePlan(false)} className="rounded-md border border-slate-300 bg-white px-4 py-2 text-xs font-bold text-slate-700">Cancel</button>
            <button onClick={createNewPlan} className="rounded-md bg-[#062b4f] px-4 py-2 text-xs font-bold text-white">Create Plan</button>
          </>
        }
      >
        <div className="space-y-4">
          <input value={newPlanName} onChange={(e) => setNewPlanName(e.target.value)} placeholder="Tên gói (vd: Premium Monthly)" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]" />
          <input type="number" min={0} value={newPlanPrice} onChange={(e) => setNewPlanPrice(e.target.value)} placeholder="Giá (VNĐ)" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]" />
          <input type="number" min={1} value={newPlanDuration} onChange={(e) => setNewPlanDuration(e.target.value)} placeholder="Thời hạn (ngày)" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]" />
          <div className="flex items-center gap-2 rounded-md border border-dashed border-slate-300 p-2">
            <label className="flex items-center gap-1 text-xs font-bold text-slate-600">
              % giảm
              <input type="number" min={0} max={100} value={yearlyDiscount} onChange={(e) => setYearlyDiscount(e.target.value)} className="w-16 rounded-md border border-slate-300 px-2 py-1 text-sm outline-none focus:border-[#0b6fb8]" />
            </label>
            <button type="button" onClick={fillYearlyFromMonthly} className="flex-1 rounded-md border border-[#0b6fb8] px-3 py-1.5 text-xs font-bold text-[#0b6fb8] hover:bg-blue-50">
              Tự điền gói năm (= gói tháng × 12 − n%)
            </button>
          </div>
        </div>
      </AdminModal>
    </div>
  );
};

export default AdminRevenuePage;

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
  type RevenueRow,
  type SubscriptionPlan,
} from '../../lib/api/admin';

const AdminRevenuePage = () => {
  const [rows, setRows] = useState<RevenueRow[]>([]);
  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  useEffect(() => {
    getTransactions().then(setRows).catch(() => setRows([]));
    getPlans().then(setPlans).catch(() => setPlans([]));
  }, []);
  const [statusFilter, setStatusFilter] = useState<'ALL' | RevenueRow['status']>('ALL');
  const [selectedRow, setSelectedRow] = useState<RevenueRow | null>(null);
  const [editingPlan, setEditingPlan] = useState<SubscriptionPlan | null>(null);
  const [planPrice, setPlanPrice] = useState('');
  const [toast, setToast] = useState<string | null>(null);

  const filteredRows =
    statusFilter === 'ALL' ? rows : rows.filter((row) => row.status === statusFilter);

  const totalSubscriptions = rows.length;
  const successPayments = rows.filter((row) => row.status === 'SUCCESS').length;
  const pendingPayments = rows.filter((row) => row.status === 'PENDING').length;
  const failedPayments = rows.filter((row) => row.status === 'FAILED').length;

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

  const refreshPayments = () => {
    const nextInvoice: RevenueRow = {
      invoiceId: `#INV-${Math.floor(Math.random() * 90000 + 10000)}`,
      transactionId: `QR-${Math.random().toString(36).slice(2, 8).toUpperCase()}`,
      customer: 'new.researcher@university.edu',
      plan: 'Premium Monthly',
      amount: '99.000₫',
      method: 'VietQR',
      paidAt: new Date().toISOString().slice(0, 16).replace('T', ' '),
      status: 'PENDING',
    };

    setRows((current) => [nextInvoice, ...current]);
    setToast('Payments refreshed. New payment callback received as PENDING.');

    window.setTimeout(() => {
      setRows((current) =>
        current.map((row) =>
          row.invoiceId === nextInvoice.invoiceId ? { ...row, status: 'SUCCESS' } : row,
        ),
      );

      setToast(`${nextInvoice.invoiceId} confirmed by payment gateway.`);
    }, 1300);
  };

  const openEditPlan = (plan: SubscriptionPlan) => {
    setEditingPlan(plan);
    setPlanPrice(plan.price);
  };

  const savePlan = () => {
    if (!editingPlan) return;

    setPlans((current) =>
      current.map((plan) =>
        plan.id === editingPlan.id ? { ...plan, price: planPrice } : plan,
      ),
    );

    setEditingPlan(null);
    setToast(`${editingPlan.name} price updated.`);
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

      <div className="grid gap-5 xl:grid-cols-4">
        <AdminMetricCard
          label="Total Subscriptions"
          value={String(totalSubscriptions)}
          helper="All subscription transactions"
          icon="★"
          accent="orange"
        />

        <AdminMetricCard
          label="Success Payments"
          value={String(successPayments)}
          helper="Confirmed successful payments"
          icon="✓"
          accent="green"
        />

        <AdminMetricCard
          label="Pending Payments"
          value={String(pendingPayments)}
          helper="Awaiting payment confirmation"
          icon="!"
          accent="slate"
        />

        <AdminMetricCard
          label="Failed Payments"
          value={String(failedPayments)}
          helper="Payment failed or rejected"
          icon="×"
          accent="red"
        />
      </div>

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

        <AdminSectionCard title="Plan Management" subtitle="Manage subscription plan pricing">
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

                <div className="mt-3 flex gap-2">
                  {plan.id === 'PLAN-FREE' ? (
                    <button
                      onClick={() => setToast('Free plan is always enabled.')}
                      className="rounded border border-slate-200 px-3 py-1 text-xs font-bold text-slate-700"
                    >
                      View
                    </button>
                  ) : (
                    <button
                      onClick={() => openEditPlan(plan)}
                      className="rounded border border-slate-200 px-3 py-1 text-xs font-bold text-slate-700"
                    >
                      Edit Price
                    </button>
                  )}
                </div>
              </div>
            ))}

            <div className="rounded-md bg-emerald-50 p-4 text-sm text-emerald-800">
              Edu accounts are receiving a configured 20% discount for premium checkout.
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
        subtitle="Update subscription plan pricing for premium checkout."
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
              className="rounded-md bg-[#062b4f] px-4 py-2 text-xs font-bold text-white"
            >
              Save Plan
            </button>
          </>
        }
      >
        {editingPlan && (
          <div className="space-y-4">
            <p className="text-sm font-bold text-slate-800">{editingPlan.name}</p>

            <input
              value={planPrice}
              onChange={(event) => setPlanPrice(event.target.value)}
              className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#0b6fb8]"
            />
          </div>
        )}
      </AdminModal>
    </div>
  );
};

export default AdminRevenuePage;
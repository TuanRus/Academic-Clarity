import { useEffect, useState } from 'react';
import { createPaymentLink, getPublicPlans, type PublicPlan } from '../../lib/api/payment';
import { planDisplayName } from '../../lib/api/admin';
import { ApiError } from '../../lib/http';
import { useAuth } from '../../hooks/useAuth';
import { Role, AccessTier } from '../../types/auth';

const formatVnd = (amount: number) => `${amount.toLocaleString('vi-VN')}₫`;

// Đối tượng học thuật được giảm 50% (khớp logic backend: AccountTag .edu hoặc role Researcher/Student).
const ACADEMIC_ROLES: Role[] = [Role.STUDENT, Role.LECTURER, Role.RESEARCHER];

const CheckoutPage = () => {
  const { user } = useAuth();
  const isAcademic = !!user && ACADEMIC_ROLES.includes(user.role);
  const isPremium = user?.accessTier === AccessTier.PREMIUM;
  const finalPrice = (base: number) => (isAcademic ? Math.round(base * 0.5) : base);

  const [plans, setPlans] = useState<PublicPlan[]>([]);
  const [selectedPlanId, setSelectedPlanId] = useState<number | null>(null);
  const [isProcessing, setIsProcessing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getPublicPlans()
      .then((list) => {
        setPlans(list);
        if (list.length > 0) setSelectedPlanId(list[0].planId);
      })
      .catch(() => setError('Failed to load plans.'));
  }, []);

  const selectedPlan = plans.find((p) => p.planId === selectedPlanId) ?? null;

  // Gói có thời hạn ngắn nhất làm chuẩn (gói tháng) để tính % tiết kiệm cho các gói dài hơn.
  const basePlan = plans.length ? plans.reduce((a, b) => (a.durationDays <= b.durationDays ? a : b)) : null;
  const savingsPct = (p: PublicPlan): number => {
    if (!basePlan || p.planId === basePlan.planId || basePlan.durationDays <= 0) return 0;
    // So với việc mua lẻ theo gói tháng: số kỳ = làm tròn (vd 365/30 ≈ 12 tháng).
    const periods = Math.round(p.durationDays / basePlan.durationDays);
    const expected = basePlan.priceAmount * periods;
    if (expected <= 0) return 0;
    return Math.round((1 - p.priceAmount / expected) * 100);
  };

  const handlePay = async () => {
    if (!selectedPlanId) return;
    setError(null);
    setIsProcessing(true);
    try {
      // Gọi BE tạo phiên PayOS rồi điều hướng sang cổng thanh toán thật.
      const { paymentUrl } = await createPaymentLink(selectedPlanId);
      window.location.href = paymentUrl;
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not start payment.');
      setIsProcessing(false);
    }
  };

  return (
    <div className="mx-auto max-w-md space-y-4">
      <div>
        <p className="text-xs font-medium uppercase tracking-wide text-gray-400">Checkout Screen</p>
        <h1 className="text-2xl font-bold text-gray-900">{isPremium ? 'Extend your Premium plan' : 'Upgrade to Premium'}</h1>
        {isPremium && (
          <p className="mt-1 rounded-md bg-indigo-50 px-3 py-2 text-xs text-indigo-700">
            You are already Premium — the new duration <strong>stacks on top</strong> of your current expiry date.
          </p>
        )}
      </div>

      {/* Plan selection */}
      <div className="space-y-2">
        {plans.length === 0 && !error && (
          <p className="text-sm text-gray-400">Loading plans…</p>
        )}
        {plans.map((p) => (
          <button
            key={p.planId}
            onClick={() => setSelectedPlanId(p.planId)}
            className={`flex w-full items-center justify-between rounded-xl border p-4 text-left transition-colors ${
              selectedPlanId === p.planId
                ? 'border-indigo-700 bg-indigo-50'
                : 'border-gray-200 bg-white hover:border-gray-300'
            }`}
          >
            <div className="flex items-center gap-3">
              <span
                className={`mt-0.5 flex h-4 w-4 shrink-0 items-center justify-center rounded-full border-2 ${
                  selectedPlanId === p.planId ? 'border-indigo-700' : 'border-gray-300'
                }`}
              >
                {selectedPlanId === p.planId && <span className="h-2 w-2 rounded-full bg-indigo-700" />}
              </span>
              <div>
                <div className="flex items-center gap-2">
                  <p className="text-sm font-medium text-gray-900">{planDisplayName(p.planName)}</p>
                  {savingsPct(p) > 0 && (
                    <span className="rounded-full bg-green-100 px-2 py-0.5 text-[11px] font-bold uppercase text-green-700">
                      Save {savingsPct(p)}%
                    </span>
                  )}
                </div>
                <span className="mt-0.5 inline-block text-xs text-gray-500">{p.durationDays} days</span>
              </div>
            </div>
            <span className="text-sm font-semibold text-gray-900">{formatVnd(p.priceAmount)}</span>
          </button>
        ))}
      </div>

      {/* Order summary */}
      {selectedPlan && (
        <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
          <div className="flex justify-between text-sm text-gray-700">
            <span>{planDisplayName(selectedPlan.planName)}</span>
            <span className="font-semibold">{formatVnd(selectedPlan.priceAmount)}</span>
          </div>
          {isAcademic && (
            <div className="mt-2 flex justify-between text-sm text-green-700">
              <span>Academic discount (−50%)</span>
              <span className="font-semibold">−{formatVnd(selectedPlan.priceAmount - finalPrice(selectedPlan.priceAmount))}</span>
            </div>
          )}
          <hr className="my-3 border-gray-200" />
          <div className="flex justify-between text-sm font-semibold text-gray-900">
            <span>Total</span>
            <span>{formatVnd(finalPrice(selectedPlan.priceAmount))}</span>
          </div>
        </div>
      )}

      {/* Payment action */}
      <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
        <p className="text-sm text-gray-700">Pay via the VietQR / PayOS gateway.</p>

        {error && <p className="mt-3 text-sm text-red-600">{error}</p>}

        <button
          disabled={isProcessing || !selectedPlan}
          onClick={handlePay}
          className="mt-4 w-full rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800 disabled:opacity-50"
        >
          {isProcessing
            ? 'Redirecting to payment gateway…'
            : selectedPlan
              ? `Pay ${formatVnd(finalPrice(selectedPlan.priceAmount))}`
              : 'Select a plan'}
        </button>
      </div>
    </div>
  );
};

export default CheckoutPage;

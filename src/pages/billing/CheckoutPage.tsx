import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { Role } from '../../types/auth';
import { getPremiumMonthlyPrice, getPremiumYearlyPrice, formatVnd } from '../../lib/pricing';

type PlanKey = 'monthly' | 'yearly';

const CheckoutPage = () => {
  const navigate = useNavigate();
  const { user } = useAuth();
  const [selectedPlan, setSelectedPlan] = useState<PlanKey>('monthly');
  const [isProcessing, setIsProcessing] = useState(false);

  // BR mới: tài khoản EDU (mail .edu) được áp giá Premium ưu đãi - xem lib/pricing.ts.
  const PLANS: Record<PlanKey, { label: string; price: string; badge?: string }> = {
    monthly: { label: 'Premium Plan (1 tháng)', price: formatVnd(getPremiumMonthlyPrice(user)) },
    yearly: {
      label: 'Premium Plan (1 năm)',
      price: formatVnd(getPremiumYearlyPrice(user)),
      badge: user?.role === Role.EDU ? 'Giá EDU · Tiết kiệm 16%' : 'Tiết kiệm 16%',
    },
  };

  const plan = PLANS[selectedPlan];

  const handlePay = (outcome: 'SUCCESS' | 'FAILED' | 'CANCELLED') => {
    setIsProcessing(true);
    setTimeout(() => {
      navigate(`/payment/return?status=${outcome}`);
    }, 600);
  };

  return (
    <div className="mx-auto max-w-md space-y-4">
      <div>
        <p className="text-xs font-medium uppercase tracking-wide text-gray-400">
          MỚI · Checkout Screen
        </p>
        <h1 className="text-2xl font-bold text-gray-900">Upgrade to Premium</h1>
      </div>

      {/* Plan selection */}
      <div className="space-y-2">
        {(Object.entries(PLANS) as [PlanKey, (typeof PLANS)[PlanKey]][]).map(([key, p]) => (
          <button
            key={key}
            onClick={() => setSelectedPlan(key)}
            className={`flex w-full items-center justify-between rounded-xl border p-4 text-left transition-colors ${
              selectedPlan === key
                ? 'border-indigo-700 bg-indigo-50'
                : 'border-gray-200 bg-white hover:border-gray-300'
            }`}
          >
            <div className="flex items-center gap-3">
              <span
                className={`mt-0.5 flex h-4 w-4 shrink-0 items-center justify-center rounded-full border-2 ${
                  selectedPlan === key ? 'border-indigo-700' : 'border-gray-300'
                }`}
              >
                {selectedPlan === key && (
                  <span className="h-2 w-2 rounded-full bg-indigo-700" />
                )}
              </span>
              <div>
                <p className="text-sm font-medium text-gray-900">{p.label}</p>
                {p.badge && (
                  <span className="mt-0.5 inline-block rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-700">
                    {p.badge}
                  </span>
                )}
              </div>
            </div>
            <span className="text-sm font-semibold text-gray-900">{p.price}</span>
          </button>
        ))}
      </div>

      {/* Order summary */}
      <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
        <div className="flex justify-between text-sm text-gray-700">
          <span>{plan.label}</span>
          <span className="font-semibold">{plan.price}</span>
        </div>
        <hr className="my-3 border-gray-200" />
        <div className="flex justify-between text-sm font-semibold text-gray-900">
          <span>Tổng cộng</span>
          <span>{plan.price}</span>
        </div>
      </div>

      {/* Payment actions */}
      <div className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
        <p className="text-sm text-gray-700">Thanh toán qua cổng VNPay (mock).</p>
        <p className="mt-1 text-xs text-gray-400">
          Khi BE nhận IPN với chữ ký hợp lệ (BR-28) → Transaction chuyển SUCCESS, tạo/cập nhật
          User Subscription, set <code>access_tier = PREMIUM</code> (BR-27a). Bên dưới là 3 nút
          demo cho 3 nhánh PENDING → SUCCESS / FAILED / CANCELLED.
        </p>

        <div className="mt-4 flex flex-col gap-2">
          <button
            disabled={isProcessing}
            onClick={() => handlePay('SUCCESS')}
            className="rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800 disabled:opacity-50"
          >
            {isProcessing ? 'Đang xử lý…' : `Pay ${plan.price} (Simulate SUCCESS)`}
          </button>
          <button
            disabled={isProcessing}
            onClick={() => handlePay('FAILED')}
            className="rounded-md border border-red-600 px-4 py-2 text-sm text-red-600 hover:bg-red-50 disabled:opacity-50"
          >
            Simulate FAILED (vnp_ResponseCode != 00)
          </button>
          <button
            disabled={isProcessing}
            onClick={() => handlePay('CANCELLED')}
            className="rounded-md border border-gray-200 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 disabled:opacity-50"
          >
            Simulate CANCELLED (đóng cổng thanh toán)
          </button>
        </div>
      </div>
    </div>
  );
};

export default CheckoutPage;
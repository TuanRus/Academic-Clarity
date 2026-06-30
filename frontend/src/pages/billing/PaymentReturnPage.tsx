import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { verifyPayment } from '../../lib/api/payment';

type Status = 'SUCCESS' | 'FAILED' | 'CANCELLED';

const COPY: Record<Status, { title: string; desc: string; tone: string }> = {
  SUCCESS: {
    title: 'Payment successful',
    desc: 'Your account has been upgraded to Premium. Advanced features are now unlocked.',
    tone: 'text-green-600',
  },
  FAILED: {
    title: 'Payment failed',
    desc: 'The transaction was unsuccessful. It has been kept for audit; you can try again.',
    tone: 'text-red-600',
  },
  CANCELLED: {
    title: 'Payment cancelled',
    desc: 'You closed the payment gateway before completing it. No changes were made to your account.',
    tone: 'text-gray-600',
  },
};

// Đích PayOS redirect về (vnp_ReturnUrl / CancelUrl).
// PayOS gắn query: ?code=00&status=PAID&cancel=false&orderCode=...
// Hỗ trợ thêm ?status=SUCCESS thủ công để test.
const resolveStatus = (params: URLSearchParams): Status => {
  const manual = params.get('status');
  if (manual === 'SUCCESS' || manual === 'PAID') return 'SUCCESS';
  if (params.get('cancel') === 'true' || manual === 'CANCELLED') return 'CANCELLED';
  if (params.get('code') === '00') return 'SUCCESS';
  if (manual === 'FAILED') return 'FAILED';
  return params.get('code') ? 'FAILED' : 'CANCELLED';
};

const PaymentReturnPage = () => {
  const [params] = useSearchParams();
  const { refreshUser } = useAuth();
  const orderCode = params.get('orderCode');
  const [status, setStatus] = useState<Status>(resolveStatus(params));
  const [syncing, setSyncing] = useState(Boolean(orderCode) || resolveStatus(params) === 'SUCCESS');
  const done = useRef(false);

  useEffect(() => {
    if (done.current) return;
    done.current = true;

    const finish = () => refreshUser().finally(() => setSyncing(false));

    // Xác nhận thẳng với BE theo orderCode (không phụ thuộc webhook public).
    // Nếu PayOS báo PAID, BE sẽ thăng cấp và trả upgraded=true.
    if (orderCode) {
      verifyPayment(orderCode)
        .then((res) => {
          if (res.upgraded) {
            setStatus('SUCCESS');
            finish();
          } else {
            // Chưa thanh toán xong → giữ trạng thái suy ra từ query (FAILED/CANCELLED/PENDING).
            setStatus((prev) => (prev === 'SUCCESS' ? 'FAILED' : prev));
            setSyncing(false);
          }
        })
        .catch(() => setSyncing(false));
    } else if (status === 'SUCCESS') {
      // Fallback: không có orderCode (vd webhook đã xử lý) → chỉ đồng bộ lại user.
      finish();
    }
  }, [orderCode, status, refreshUser]);

  const copy = COPY[status];

  return (
    <div className="mx-auto max-w-md space-y-4 text-center">
      <p className="text-xs font-medium uppercase tracking-wide text-gray-400">Payment Return Screen</p>
      <div className="rounded-xl border border-gray-200 bg-white p-8 shadow-sm">
        <h1 className={`text-xl font-bold ${copy.tone}`}>{copy.title}</h1>
        <p className="mt-2 text-sm text-gray-600">{copy.desc}</p>

        {status === 'SUCCESS' && syncing && (
          <p className="mt-3 text-xs text-gray-400">Syncing your account status…</p>
        )}

        <Link
          to={status === 'SUCCESS' ? '/dashboard' : '/pricing'}
          className="mt-6 inline-block rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800"
        >
          {status === 'SUCCESS' ? 'Go to Dashboard' : 'Back to Pricing'}
        </Link>
      </div>
    </div>
  );
};

export default PaymentReturnPage;

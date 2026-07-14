import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';

type Status = 'SUCCESS' | 'FAILED' | 'CANCELLED';

const COPY: Record<Status, { title: string; desc: string; tone: string }> = {
  SUCCESS: {
    title: 'Thanh toán thành công',
    desc: 'Tài khoản của bạn đã được nâng cấp lên Premium. Các tính năng FR-11..22 đã được mở khoá.',
    tone: 'text-green-600',
  },
  FAILED: {
    title: 'Thanh toán thất bại',
    desc: 'Giao dịch không thành công (vnp_ResponseCode != 00). Transaction được giữ lại cho audit (BR-30), bạn có thể thử lại.',
    tone: 'text-red-600',
  },
  CANCELLED: {
    title: 'Đã hủy thanh toán',
    desc: 'Bạn đã đóng cổng thanh toán trước khi hoàn tất. subscription_id vẫn NULL, không có thay đổi nào với tài khoản.',
    tone: 'text-gray-600',
  },
};

// MỚI · Payment Return Screen
// Đích đến sau khi cổng thanh toán redirect về (vnp_ReturnUrl).
// Map đúng 3 nhánh PENDING -> SUCCESS/FAILED/CANCELLED của state diagram (mục 4).
const PaymentReturnPage = () => {
  const [params] = useSearchParams();
  const { upgradeToPremium } = useAuth();
  const status = (params.get('status') as Status) ?? 'FAILED';
  const [applied, setApplied] = useState(false);

  useEffect(() => {
    if (status === 'SUCCESS' && !applied) {
      // BR-27a: IPN verify OK -> set access_tier = PREMIUM
      upgradeToPremium('2026-12-31');
      setApplied(true);
    }
  }, [status, applied, upgradeToPremium]);

  const copy = COPY[status];

  return (
    <div className="mx-auto max-w-md space-y-4 text-center">
      <p className="text-xs font-medium uppercase tracking-wide text-gray-400">
        MỚI · Payment Return Screen
      </p>
      <div className="rounded-xl border border-gray-200 bg-white p-8 shadow-sm">
        <h1 className={`text-xl font-bold ${copy.tone}`}>{copy.title}</h1>
        <p className="mt-2 text-sm text-gray-600">{copy.desc}</p>

        <Link
          to={status === 'SUCCESS' ? '/dashboard' : '/pricing'}
          className="mt-6 inline-block rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800"
        >
          {status === 'SUCCESS' ? 'Đi tới Dashboard' : 'Quay lại Pricing'}
        </Link>
      </div>
    </div>
  );
};

export default PaymentReturnPage;

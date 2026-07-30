import { createContext, useCallback, useContext, useRef, useState, type ReactNode } from 'react';

type ToastVariant = 'success' | 'error';

interface ToastItem {
  id: number;
  message: string;
  variant: ToastVariant;
}

interface AdminToastContextValue {
  showSuccess: (message: string) => void;
  showError: (message: string) => void;
}

const AdminToastContext = createContext<AdminToastContextValue | null>(null);

const TOAST_DURATION_MS = 4000;

const toastStyle: Record<ToastVariant, { border: string; badge: string; icon: string }> = {
  success: { border: 'border-emerald-200', badge: 'bg-emerald-100 text-emerald-700', icon: '✓' },
  error: { border: 'border-red-200', badge: 'bg-red-100 text-red-700', icon: '✕' },
};

// Toast provider toàn cục cho khu Admin — mount 1 lần ở AdminLayout, mọi trang con gọi useAdminToast().
export const AdminToastProvider = ({ children }: { children: ReactNode }) => {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const nextId = useRef(0);

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((t) => t.id !== id));
  }, []);

  const show = useCallback((message: string, variant: ToastVariant) => {
    const id = nextId.current++;
    setToasts((current) => [...current, { id, message, variant }]);
    window.setTimeout(() => dismiss(id), TOAST_DURATION_MS);
  }, [dismiss]);

  const showSuccess = useCallback((message: string) => show(message, 'success'), [show]);
  const showError = useCallback((message: string) => show(message, 'error'), [show]);

  return (
    <AdminToastContext.Provider value={{ showSuccess, showError }}>
      {children}
      <div className="fixed right-6 top-20 z-50 flex max-w-sm flex-col gap-2">
        {toasts.map((t) => (
          <div
            key={t.id}
            className={`rounded-lg border ${toastStyle[t.variant].border} bg-white px-4 py-3 text-sm font-semibold text-slate-800 shadow-xl`}
          >
            <div className="flex items-start gap-3">
              <span className={`mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full text-xs ${toastStyle[t.variant].badge}`}>
                {toastStyle[t.variant].icon}
              </span>
              <p className="flex-1">{t.message}</p>
              <button type="button" onClick={() => dismiss(t.id)} className="text-slate-400 hover:text-slate-700">×</button>
            </div>
          </div>
        ))}
      </div>
    </AdminToastContext.Provider>
  );
};

export const useAdminToast = (): AdminToastContextValue => {
  const ctx = useContext(AdminToastContext);
  if (!ctx) throw new Error('useAdminToast must be used within AdminToastProvider');
  return ctx;
};

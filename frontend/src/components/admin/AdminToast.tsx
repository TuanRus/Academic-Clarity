interface AdminToastProps {
  message: string | null;
  onClose: () => void;
}

const AdminToast = ({ message, onClose }: AdminToastProps) => {
  if (!message) return null;

  return (
    <div className="fixed right-6 top-20 z-50 max-w-sm rounded-lg border border-emerald-200 bg-white px-4 py-3 text-sm font-semibold text-gray-800 shadow-xl">
      <div className="flex items-start gap-3">
        <span className="mt-0.5 flex h-5 w-5 items-center justify-center rounded-full bg-emerald-100 text-xs text-emerald-700">✓</span>
        <p className="flex-1">{message}</p>
        <button type="button" onClick={onClose} className="text-gray-400 hover:text-gray-700">×</button>
      </div>
    </div>
  );
};

export default AdminToast;

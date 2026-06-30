import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

/**
 * Bọc quanh các route cần đăng nhập (FR-02/FR-04).
 * Nếu chưa có user -> redirect về /login, giữ lại "from" để quay lại sau khi login.
 */
const RequireAuth = () => {
  const { user, loading } = useAuth();
  const location = useLocation();

  // Đang khôi phục phiên từ token → chờ, tránh redirect sớm về /login.
  if (loading) {
    return <div className="flex min-h-screen items-center justify-center text-sm text-gray-400">Đang tải…</div>;
  }

  if (!user) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <Outlet />;
};

export default RequireAuth;

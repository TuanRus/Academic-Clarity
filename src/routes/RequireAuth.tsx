import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

/**
 * Bọc quanh các route cần đăng nhập (FR-02/FR-04).
 * Nếu chưa có user -> redirect về /login, giữ lại "from" để quay lại sau khi login.
 */
const RequireAuth = () => {
  const { user } = useAuth();
  const location = useLocation();

  if (!user) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <Outlet />;
};

export default RequireAuth;

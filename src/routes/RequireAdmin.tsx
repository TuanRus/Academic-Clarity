import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { isAdmin } from '../lib/permissions';

/**
 * FR-27/28: bọc quanh khu vực quản trị (/admin/**).
 * Quyền vào chỉ dựa vào role = ADMIN, độc lập hoàn toàn với accessTier (xem isAdmin()).
 * Khác với RequireFeature (gate từng phần UI), đây là route guard: không phải ADMIN -> đá về Home.
 */
const RequireAdmin = () => {
  const { user } = useAuth();

  if (!isAdmin(user)) {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
};

export default RequireAdmin;

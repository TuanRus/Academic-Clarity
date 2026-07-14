import { Routes, Route, Navigate } from 'react-router-dom';
import MainLayout from '../components/layout/MainLayout';
import RequireAuth from './RequireAuth';
import RequireAdmin from './RequireAdmin';

import LoginPage from '../pages/auth/LoginPage';
import RegisterPage from '../pages/auth/RegisterPage';
import HomePage from '../pages/home/HomePage';
import ProfilePage from '../pages/profile/ProfilePage';
import StandardSearchPage from '../pages/search/StandardSearchPage';
import AdvancedSearchPage from '../pages/search/AdvancedSearchPage';
import PaperDetailPage from '../pages/papers/PaperDetailPage';
import SavedLibraryPage from '../pages/library/SavedLibraryPage';
import NotificationCenterPage from '../pages/notifications/NotificationCenterPage';
import ResearchLandscapePage from '../pages/landscape/ResearchLandscapePage';
import TrendDashboardPage from '../pages/dashboard/TrendDashboardPage';
import PricingPage from '../pages/billing/PricingPage';
import CheckoutPage from '../pages/billing/CheckoutPage';
import PaymentReturnPage from '../pages/billing/PaymentReturnPage';
import AdminDashboardPage from '../pages/admin/AdminDashboardPage';

const AppRoutes = () => {
  return (
    <Routes>
      {/* S-01, S-02: public, không cần đăng nhập */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      {/* Mọi route còn lại yêu cầu đăng nhập (FR-02/04) */}
      <Route element={<RequireAuth />}>
        <Route element={<MainLayout />}>
          <Route path="/" element={<HomePage />} />
          <Route path="/profile" element={<ProfilePage />} />

          <Route path="/search" element={<StandardSearchPage />} />
          <Route path="/search/advanced" element={<AdvancedSearchPage />} />

          <Route path="/papers/:paperId" element={<PaperDetailPage />} />
          <Route path="/library" element={<SavedLibraryPage />} />
          <Route path="/notifications" element={<NotificationCenterPage />} />

          {/* FR-11..15 - gating từng phần bên trong page bằng <RequireFeature> */}
          <Route path="/landscape" element={<ResearchLandscapePage />} />
          {/* FR-19..22 - gating từng phần bên trong page bằng <RequireFeature> */}
          <Route path="/dashboard" element={<TrendDashboardPage />} />

          {/* MỚI: luồng Premium / Payment (BR-26..31) */}
          <Route path="/pricing" element={<PricingPage />} />
          <Route path="/checkout" element={<CheckoutPage />} />
          <Route path="/payment/return" element={<PaymentReturnPage />} />

          {/* FR-27/28: khu vực quản trị - chỉ role = ADMIN truy cập được */}
          <Route element={<RequireAdmin />}>
            <Route path="/admin" element={<AdminDashboardPage />} />
          </Route>
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
};

export default AppRoutes;

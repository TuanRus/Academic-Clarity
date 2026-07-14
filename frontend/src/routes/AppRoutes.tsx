import { Routes, Route, Navigate } from 'react-router-dom';
import MainLayout from '../components/layout/MainLayout';
import RequireAuth from './RequireAuth';

import LoginPage from '../pages/auth/LoginPage';
import RegisterPage from '../pages/auth/RegisterPage';
import ForgotPasswordPage from '../pages/auth/ForgotPasswordPage';
import HomePage from '../pages/home/HomePage';
import ProfilePage from '../pages/profile/ProfilePage';
import StandardSearchPage from '../pages/search/StandardSearchPage';
import AdvancedSearchPage from '../pages/search/AdvancedSearchPage';
import PaperDetailPage from '../pages/papers/PaperDetailPage';
import SavedLibraryPage from '../pages/library/SavedLibraryPage';
import NotificationCenterPage from '../pages/notifications/NotificationCenterPage';
import ResearchLandscapePage from '../pages/landscape/ResearchLandscapePage';
import TrendDashboardPage from '../pages/dashboard/TrendDashboardPage';
import OverlapCheckerPage from '../pages/idea/OverlapCheckerPage';
import PricingPage from '../pages/billing/PricingPage';
import CheckoutPage from '../pages/billing/CheckoutPage';
import PaymentReturnPage from '../pages/billing/PaymentReturnPage';
import AdminLayout from '../components/admin/AdminLayout';
import AdminDashboardPage from '../pages/admin/AdminDashboardPage';
import AdminPipelinesPage from '../pages/admin/AdminPipelinesPage';
import AdminRepositoryPage from '../pages/admin/AdminRepositoryPage';
import AdminUsersPage from '../pages/admin/AdminUsersPage';
import AdminRevenuePage from '../pages/admin/AdminRevenuePage';
import AdminActivityLogsPage from '../pages/admin/AdminActivityLogsPage';
import AdminSettingsPage from '../pages/admin/AdminSettingsPage';

const AppRoutes = () => {
  return (
    <Routes>
      {/* S-01, S-02: public, không cần đăng nhập */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />

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
          {/* Premium: Idea Overlap Checker - gate bằng <RequireFeature OVERLAP_CHECK> trong page */}
          <Route path="/overlap" element={<OverlapCheckerPage />} />

          {/* MỚI: luồng Premium / Payment (BR-26..31) */}
          <Route path="/pricing" element={<PricingPage />} />
          <Route path="/checkout" element={<CheckoutPage />} />
          <Route path="/payment/return" element={<PaymentReturnPage />} />
        </Route>

        <Route path="/admin" element={<AdminLayout />}>
          <Route index element={<AdminDashboardPage />} />
          <Route path="pipelines" element={<AdminPipelinesPage />} />
          <Route path="repository" element={<AdminRepositoryPage />} />
          <Route path="users" element={<AdminUsersPage />} />
          <Route path="revenue" element={<AdminRevenuePage />} />
          <Route path="logs" element={<AdminActivityLogsPage />} />
          <Route path="settings" element={<AdminSettingsPage />} />
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
};

export default AppRoutes;

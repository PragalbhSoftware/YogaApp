import { lazy, Suspense } from "react";
import { BrowserRouter, Route, Routes } from "react-router-dom";
import { CircularProgress } from "@mui/material";
import { AppErrorBoundary } from "@/app/router/app-error-boundary";
import { NotFoundPage } from "@/app/router/not-found-page";
import { AdminLayout } from "@/app/layouts/admin-layout";
import { AuthLayout } from "@/app/layouts/auth-layout";
import { CustomerLayout } from "@/app/layouts/customer-layout";
import { InstructorLayout } from "@/app/layouts/instructor-layout";
import { AllowGuestOr, RedirectIfSignedIn, RequireAuth, RequireRole } from "@/app/router/guards";
import { routes } from "@/constants/routes";
import { ConsentPrompt } from "@/features/auth/components/consent-prompt";

const LoginPage = lazy(() =>
  import("@/features/auth/pages/login-page").then((module) => ({ default: module.LoginPage })),
);
const DashboardPage = lazy(() =>
  import("@/features/admin/pages/dashboard-page").then((module) => ({ default: module.DashboardPage })),
);
const ApprovalsPage = lazy(() =>
  import("@/features/admin/pages/approvals-page").then((module) => ({ default: module.ApprovalsPage })),
);
const UsersPage = lazy(() =>
  import("@/features/admin/pages/users-page").then((module) => ({ default: module.UsersPage })),
);
const UserDetailPage = lazy(() =>
  import("@/features/admin/pages/user-detail-page").then((module) => ({ default: module.UserDetailPage })),
);
const AdminBookingsPage = lazy(() =>
  import("@/features/admin/pages/bookings-page").then((module) => ({ default: module.BookingsPage })),
);
const AdminBookingDetailPage = lazy(() =>
  import("@/features/admin/pages/booking-detail-page").then((module) => ({
    default: module.BookingDetailPage,
  })),
);
const TransactionsPage = lazy(() =>
  import("@/features/admin/pages/transactions-page").then((module) => ({
    default: module.TransactionsPage,
  })),
);
const JobsPage = lazy(() =>
  import("@/features/admin/pages/jobs-page").then((module) => ({
    default: module.JobsPage,
  })),
);
const ErrorsPage = lazy(() =>
  import("@/features/admin/pages/errors-page").then((module) => ({
    default: module.ErrorsPage,
  })),
);
const SettingsPage = lazy(() =>
  import("@/features/admin/pages/settings-page").then((module) => ({ default: module.SettingsPage })),
);
const HomePage = lazy(() =>
  import("@/features/marketplace/pages/home-page").then((module) => ({ default: module.HomePage })),
);
const TeacherPage = lazy(() =>
  import("@/features/marketplace/pages/teacher-page").then((module) => ({
    default: module.TeacherPage,
  })),
);
const BookPage = lazy(() =>
  import("@/features/bookings/pages/book-page").then((module) => ({ default: module.BookPage })),
);
const BookingsPage = lazy(() =>
  import("@/features/bookings/pages/bookings-page").then((module) => ({
    default: module.BookingsPage,
  })),
);
const PacksPage = lazy(() =>
  import("@/features/credits/pages/packs-page").then((module) => ({ default: module.PacksPage })),
);
const ProfilePage = lazy(() =>
  import("@/features/profile/pages/profile-page").then((module) => ({
    default: module.ProfilePage,
  })),
);
const SchedulePage = lazy(() =>
  import("@/features/instructor/pages/schedule-page").then((module) => ({
    default: module.SchedulePage,
  })),
);
const InstructorRequestsPage = lazy(() =>
  import("@/features/instructor/pages/instructor-requests-page").then((module) => ({
    default: module.InstructorRequestsPage,
  })),
);
const InstructorEarningsPage = lazy(() =>
  import("@/features/instructor/pages/earnings-page").then((module) => ({
    default: module.InstructorEarningsPage,
  })),
);
const InstructorServicesPage = lazy(() =>
  import("@/features/instructor/pages/services-page").then((module) => ({
    default: module.InstructorServicesPage,
  })),
);
const InstructorProfilePage = lazy(() =>
  import("@/features/instructor/pages/instructor-profile-page").then((module) => ({
    default: module.InstructorProfilePage,
  })),
);
const InstructorRegisterPage = lazy(() =>
  import("@/features/instructor/pages/register-page").then((module) => ({
    default: module.InstructorRegisterPage,
  })),
);

const RefundPolicyPage = lazy(() =>
  import("@/features/content/pages/content-page").then((module) => ({ default: module.RefundPolicyPage })),
);
const TermsPage = lazy(() =>
  import("@/features/content/pages/content-page").then((module) => ({ default: module.TermsPage })),
);
const PrivacyPage = lazy(() =>
  import("@/features/content/pages/content-page").then((module) => ({ default: module.PrivacyPage })),
);

export function RouteFallback() {
  return (
    <div className="flex min-h-svh items-center justify-center bg-brand-background">
      <CircularProgress />
    </div>
  );
}

export function AppRouter() {
  return (
    <BrowserRouter>
      <AppErrorBoundary>
        <Suspense fallback={<RouteFallback />}>
          <Routes>
            <Route element={<RedirectIfSignedIn />}>
              <Route element={<AuthLayout />}>
                <Route path={routes.login} element={<LoginPage />} />
              </Route>
            </Route>
            <Route element={<AllowGuestOr roles={["Customer"]} />}>
              <Route element={<CustomerLayout />}>
                <Route path={routes.home} element={<HomePage />} />
                <Route path={routes.teacher} element={<TeacherPage />} />
                <Route path={routes.packs} element={<PacksPage />} />
                <Route element={<RequireAuth />}>
                  <Route element={<RequireRole roles={["Customer"]} />}>
                    <Route path={routes.book} element={<BookPage />} />
                    <Route path={routes.bookings} element={<BookingsPage />} />
                    <Route path={routes.profile} element={<ProfilePage />} />
                    <Route path={routes.instructorRegister} element={<InstructorRegisterPage />} />
                  </Route>
                </Route>
              </Route>
            </Route>
            <Route element={<CustomerLayout />}>
              <Route path={routes.refundPolicy} element={<RefundPolicyPage />} />
              <Route path={routes.terms} element={<TermsPage />} />
              <Route path={routes.privacy} element={<PrivacyPage />} />
            </Route>
            <Route element={<RequireAuth />}>
              <Route element={<RequireRole roles={["Provider"]} />}>
                <Route element={<InstructorLayout />}>
                  <Route path={routes.instructor} element={<SchedulePage />} />
                  <Route path={routes.instructorBookings} element={<InstructorRequestsPage />} />
                  <Route path={routes.instructorEarnings} element={<InstructorEarningsPage />} />
                  <Route path={routes.instructorServices} element={<InstructorServicesPage />} />
                  <Route path={routes.instructorProfile} element={<InstructorProfilePage />} />
                </Route>
              </Route>
              <Route element={<RequireRole roles={["Admin"]} />}>
                <Route element={<AdminLayout />}>
                  <Route path={routes.admin} element={<DashboardPage />} />
                  <Route path={routes.adminApprovals} element={<ApprovalsPage />} />
                  <Route path={routes.adminUsers} element={<UsersPage />} />
                  <Route path={routes.adminUser} element={<UserDetailPage />} />
                  <Route path={routes.adminBookings} element={<AdminBookingsPage />} />
                  <Route path={routes.adminBooking} element={<AdminBookingDetailPage />} />
                  <Route path={routes.adminTransactions} element={<TransactionsPage />} />
                  <Route path={routes.adminJobs} element={<JobsPage />} />
                  <Route path={routes.adminErrors} element={<ErrorsPage />} />
                  <Route path={routes.adminMasters} element={<SettingsPage />} />
                </Route>
              </Route>
            </Route>
            <Route path="*" element={<NotFoundPage />} />
          </Routes>
          <ConsentPrompt />
        </Suspense>
      </AppErrorBoundary>
    </BrowserRouter>
  );
}

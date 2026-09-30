export const routes = {
  login: "/login",
  home: "/",
  explore: "/explore",
  instructors: "/teachers",
  teacher: "/teachers/:id",
  book: "/book/:providerId/:slotId",
  bookings: "/bookings",
  profile: "/profile",
  admin: "/admin",
  adminApprovals: "/admin/approvals",
  adminUsers: "/admin/users",
  adminUser: "/admin/users/:id",
  adminBookings: "/admin/bookings",
  adminBooking: "/admin/bookings/:id",
  adminTransactions: "/admin/transactions",
  adminMasters: "/admin/masters",
  instructor: "/instructor",
  instructorRegister: "/instructor/register",
  instructorBookings: "/instructor/bookings",
  instructorEarnings: "/instructor/earnings",
  instructorProfile: "/instructor/profile",
} as const;

export function teacherPath(id: string) {
  return `${routes.instructors}/${id}`;
}

export function bookPath(providerId: string, slotId: string, mode: string) {
  return `/book/${providerId}/${slotId}?mode=${encodeURIComponent(mode)}`;
}

export function adminUserPath(id: string) {
  return `${routes.adminUsers}/${id}`;
}

export function adminBookingPath(id: string) {
  return `${routes.adminBookings}/${id}`;
}

export function afterSignInPath(role: string | null | undefined) {
  if (role === "Admin") return routes.admin;
  if (role === "Provider") return routes.instructor;
  return routes.home;
}

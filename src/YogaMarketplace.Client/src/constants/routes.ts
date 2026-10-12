export const routes = {
  login: "/login",
  home: "/",
  explore: "/explore",
  instructors: "/teachers",
  teacher: "/teachers/:id",
  book: "/book/:providerId/:slotId",
  bookings: "/bookings",
  profile: "/profile",
  refundPolicy: "/refund-policy",
  terms: "/terms",
  privacy: "/privacy",
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

/** Where the guard was sending the visitor when it asked them to sign in. */
export type SignInFrom = { pathname: string; search?: string; hash?: string };

/**
 * A customer goes back to the page that asked them to sign in (for example the slot they were booking).
 * Other roles, or an unsafe or missing path, go to the role's home.
 */
export function signInReturnPath(role: string | null | undefined, from: SignInFrom | null | undefined) {
  const fallback = afterSignInPath(role);
  if (role !== "Customer" || !from?.pathname) return fallback;
  const { pathname } = from;
  if (!pathname.startsWith("/") || pathname.startsWith("//") || pathname === routes.login) return fallback;
  const otherRoleArea =
    pathname.startsWith(routes.admin) ||
    (pathname.startsWith(routes.instructor) && pathname !== routes.instructorRegister);
  if (otherRoleArea) return fallback;
  return `${pathname}${from.search ?? ""}${from.hash ?? ""}`;
}

export function readSignInFrom(state: unknown): SignInFrom | null {
  if (!state || typeof state !== "object" || !("from" in state)) return null;
  const from = (state as { from: unknown }).from;
  if (!from || typeof from !== "object" || typeof (from as SignInFrom).pathname !== "string") return null;
  return from as SignInFrom;
}

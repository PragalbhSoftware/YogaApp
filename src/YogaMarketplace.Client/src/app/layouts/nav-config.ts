import type { LucideIcon } from "lucide-react";
import {
  CalendarDays,
  Compass,
  Home,
  Inbox,
  Layers,
  LogIn,
  User,
  Wallet,
} from "lucide-react";
import { routes } from "@/constants/routes";

/** One bottom/desktop tab. Labels live here so category copy can change in one place. */
export type NavItem = {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Exact path only (e.g. `/` or `/instructor`). */
  end?: boolean;
  /** Custom active match when prefix rules are not enough. */
  match?: (pathname: string) => boolean;
};

/** Paths that have a registered `<Route>`. Tabs whose `to` is missing are omitted. */
export const registeredNavPaths = new Set<string>([
  routes.home,
  routes.explore,
  routes.login,
  routes.bookings,
  routes.packs,
  routes.profile,
  routes.instructor,
  routes.instructorBookings,
  routes.instructorEarnings,
  routes.instructorServices,
  routes.instructorProfile,
]);

export const guestNavItems: NavItem[] = [
  { to: routes.home, label: "Home", icon: Home, end: true },
  { to: routes.login, label: "Sign in", icon: LogIn, end: true },
];

export const customerNavItems: NavItem[] = [
  { to: routes.home, label: "Home", icon: Home, end: true },
  {
    to: routes.explore,
    label: "Explore",
    icon: Compass,
    match: (pathname) =>
      pathname === routes.explore || pathname.startsWith(`${routes.instructors}/`),
  },
  { to: routes.bookings, label: "Bookings", icon: CalendarDays },
  { to: routes.packs, label: "Credits", icon: Wallet },
  { to: routes.profile, label: "Profile", icon: User },
];

export const instructorNavItems: NavItem[] = [
  { to: routes.instructor, label: "Schedule", icon: CalendarDays, end: true },
  { to: routes.instructorBookings, label: "Requests", icon: Inbox },
  { to: routes.instructorEarnings, label: "Earnings", icon: Wallet },
  { to: routes.instructorServices, label: "Services", icon: Layers },
  { to: routes.instructorProfile, label: "Profile", icon: User },
];

export function filterRegisteredNavItems(items: NavItem[]): NavItem[] {
  return items.filter((item) => registeredNavPaths.has(item.to));
}

export function isNavItemActive(pathname: string, item: NavItem): boolean {
  if (item.match) return item.match(pathname);
  if (item.end) return pathname === item.to;
  return pathname === item.to || pathname.startsWith(`${item.to}/`);
}

/** Hide the mobile bar on the booking checkout flow (`/book/...`), not on `/bookings`. */
export function shouldShowBottomTabBar(pathname: string): boolean {
  return pathname !== "/book" && !pathname.startsWith("/book/");
}

/** Content spacer when the mobile tab bar is visible. Cleared at `md+`. */
export const tabBarContentPaddingClass =
  "pb-[calc(4.75rem+env(safe-area-inset-bottom))] md:pb-0";

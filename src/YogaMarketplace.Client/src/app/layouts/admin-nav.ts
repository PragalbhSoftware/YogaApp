import type { LucideIcon } from "lucide-react";
import {
  Bell,
  BookOpen,
  Building2,
  CalendarClock,
  Coins,
  FileText,
  Image,
  IndianRupee,
  KeyRound,
  LayoutDashboard,
  Map,
  MapPin,
  Package,
  Receipt,
  Scale,
  Settings2,
  Shield,
  ShieldCheck,
  Tags,
  TriangleAlert,
  Users,
  Wallet,
} from "lucide-react";
import { routes } from "@/constants/routes";

export type AdminNavBadge = "errors";

/** One row per admin screen. A new page is one entry here and one route. */
export type AdminNavItem = {
  group: string;
  label: string;
  to: string;
  icon: LucideIcon;
  end?: boolean;
  badge?: AdminNavBadge;
};

export const adminNav: AdminNavItem[] = [
  { group: "Dashboard", label: "Dashboard", to: routes.admin, icon: LayoutDashboard, end: true },
  { group: "Operations", label: "Approvals", to: routes.adminApprovals, icon: ShieldCheck },
  { group: "Operations", label: "Bookings", to: routes.adminBookings, icon: BookOpen },
  { group: "Operations", label: "Users", to: routes.adminUsers, icon: Users },
  { group: "Operations", label: "Errors", to: routes.adminErrors, icon: TriangleAlert, badge: "errors" },
  { group: "Operations", label: "Jobs", to: routes.adminJobs, icon: CalendarClock },
  { group: "Finance", label: "Transactions", to: routes.adminTransactions, icon: Receipt },
  { group: "Finance", label: "Payouts", to: routes.adminPayouts, icon: Wallet },
  { group: "Finance", label: "Reconciliation", to: routes.adminReconciliation, icon: Scale },
  { group: "Finance", label: "Fees and payouts", to: routes.adminFinanceFees, icon: IndianRupee },
  { group: "Finance", label: "Reconciliation settings", to: routes.adminReconciliationSettings, icon: Settings2 },
  { group: "Locations", label: "States", to: routes.adminStates, icon: Map },
  { group: "Locations", label: "Cities", to: routes.adminCities, icon: Building2 },
  { group: "Locations", label: "Areas", to: routes.adminAreas, icon: MapPin },
  { group: "Catalog", label: "Categories", to: routes.adminCategories, icon: Tags },
  { group: "Catalog", label: "Session packs", to: routes.adminPacks, icon: Package },
  { group: "Catalog", label: "Credit rules", to: routes.adminCreditRules, icon: Coins },
  { group: "Content", label: "Policy pages", to: routes.adminContentPages, icon: FileText },
  { group: "Content", label: "Home banner", to: routes.adminBanner, icon: Image },
  { group: "Content", label: "Consent and data", to: routes.adminConsent, icon: Shield },
  { group: "Settings", label: "Sign-in limits", to: routes.adminSignIn, icon: KeyRound },
  { group: "Settings", label: "Alerts", to: routes.adminAlerts, icon: Bell },
];

export function adminNavGroups(items: AdminNavItem[]) {
  const groups: { name: string; items: AdminNavItem[] }[] = [];
  for (const item of items) {
    const current = groups.at(-1);
    if (current?.name === item.group) current.items.push(item);
    else groups.push({ name: item.group, items: [item] });
  }
  return groups;
}

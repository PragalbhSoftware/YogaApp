import { routes } from "@/constants/routes";

const mastersTabs: Record<string, string> = {
  earnings: routes.adminFinanceFees,
  packs: routes.adminPacks,
  states: routes.adminStates,
  cities: routes.adminCities,
  areas: routes.adminAreas,
  banner: routes.adminBanner,
  catalog: routes.adminCategories,
  pages: routes.adminContentPages,
  alerts: routes.adminAlerts,
  "sign-in": routes.adminSignIn,
};

const moved: [string, string][] = [
  ["/admin/approvals", routes.adminApprovals],
  ["/admin/bookings", routes.adminBookings],
  ["/admin/users", routes.adminUsers],
  ["/admin/jobs", routes.adminJobs],
  ["/admin/errors", routes.adminErrors],
];

/** Old admin paths, including `?tab=`, land on the page that replaced them. */
export function legacyAdminPath(pathname: string, search: string) {
  const params = new URLSearchParams(search.startsWith("?") ? search.slice(1) : search);
  if (pathname === "/admin/transactions") {
    const tab = params.get("tab");
    params.delete("tab");
    const dest =
      tab === "payouts"
        ? routes.adminPayouts
        : tab === "reconciliation"
          ? routes.adminReconciliation
          : routes.adminTransactions;
    return withSearch(dest, params);
  }
  if (pathname === "/admin/masters") {
    const tab = params.get("tab") ?? "earnings";
    params.delete("tab");
    return withSearch(mastersTabs[tab] ?? routes.adminFinanceFees, params);
  }
  for (const [from, to] of moved) {
    if (pathname === from || pathname.startsWith(`${from}/`))
      return withSearch(to + pathname.slice(from.length), params);
  }
  return routes.admin;
}

function withSearch(path: string, params: URLSearchParams) {
  const query = params.toString();
  return query ? `${path}?${query}` : path;
}

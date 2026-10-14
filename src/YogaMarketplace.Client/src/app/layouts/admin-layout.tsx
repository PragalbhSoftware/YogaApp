import { Suspense, type ComponentType } from "react";
import { NavLink, Outlet, useLocation } from "react-router-dom";
import { Badge, Button } from "@mui/material";
import {
  BookOpen,
  CalendarClock,
  LayoutDashboard,
  Receipt,
  Settings,
  ShieldCheck,
  TriangleAlert,
  Users,
} from "lucide-react";
import { PageLoader } from "@/components/common/page-loader";
import { routes } from "@/constants/routes";
import { useAuth } from "@/features/auth/hooks/use-auth";
import { useSignOut } from "@/features/auth/hooks/use-sign-out";
import { useAdminErrorSummary } from "@/features/admin/hooks/use-admin";
import { displayName } from "@/features/admin/utils";
import { cn } from "@/utils/cn";

const navItems = [
  { to: routes.admin, label: "Dashboard", icon: LayoutDashboard, end: true },
  { to: routes.adminApprovals, label: "Approvals", icon: ShieldCheck, end: false },
  { to: routes.adminUsers, label: "Users", icon: Users, end: false },
  { to: routes.adminBookings, label: "Bookings", icon: BookOpen, end: false },
  { to: routes.adminTransactions, label: "Money", icon: Receipt, end: false },
  { to: routes.adminJobs, label: "Jobs", icon: CalendarClock, end: false },
  { to: routes.adminErrors, label: "Errors", icon: TriangleAlert, end: false },
  { to: routes.adminMasters, label: "Settings", icon: Settings, end: false },
];

const badgeMax = 99;

function isNavActive(pathname: string, to: string, end: boolean) {
  if (end) return pathname === to;
  return pathname === to || pathname.startsWith(`${to}/`);
}

/** The nav icon, with the open error count on the Errors item. */
function NavIcon({ icon: Icon, count, className }: { icon: ComponentType<{ className?: string }>; count: number; className: string }) {
  return (
    <>
      <Badge badgeContent={count} color="error" max={badgeMax}>
        <Icon className={className} aria-hidden="true" />
      </Badge>
      {count > 0 ? <span className="sr-only">{`${count} open`}</span> : null}
    </>
  );
}

export function AdminLayout() {
  const { pathname } = useLocation();
  const { user } = useAuth();
  const onSignOut = useSignOut();
  const ownerName = displayName(user?.name, user?.phone ?? "");
  const openErrors = useAdminErrorSummary().data?.unacknowledged ?? 0;
  const countFor = (to: string) => (to === routes.adminErrors ? openErrors : 0);

  return (
    <div className="min-h-svh bg-brand-background lg:flex">
      <aside className="hidden lg:flex lg:w-64 lg:shrink-0 lg:flex-col lg:border-r lg:border-brand-border lg:bg-brand-surface">
        <div className="px-5 py-6">
          <p className="font-heading text-lg text-brand-primary">Yoga Marketplace</p>
          <p className="mt-0.5 text-xs font-medium tracking-[0.16em] text-brand-muted uppercase">Owner</p>
        </div>
        <nav aria-label="Owner" className="flex-1 space-y-1 px-3">
          {navItems.map((item) => {
            const active = isNavActive(pathname, item.to, item.end);
            return (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                aria-current={active ? "page" : undefined}
                className={cn(
                  "flex items-center gap-3 rounded-2xl px-3 py-2.5 text-sm font-medium",
                  active ? "bg-brand-background text-brand-primary" : "text-brand-muted",
                )}
              >
                <NavIcon icon={item.icon} count={countFor(item.to)} className="size-4 shrink-0" />
                {item.label}
              </NavLink>
            );
          })}
        </nav>
        <div className="border-t border-brand-border px-5 py-4">
          <p className="truncate text-sm font-medium">{ownerName}</p>
          <Button variant="text" onClick={onSignOut} sx={{ minHeight: 40, px: 0, mt: 0.5 }}>
            Sign out
          </Button>
        </div>
      </aside>

      <div className="min-w-0 flex-1">
        <header className="sticky top-0 z-20 border-b border-brand-border bg-brand-background/95 backdrop-blur lg:hidden">
          <div className="flex items-center justify-between gap-3 px-4 py-3">
            <div className="min-w-0">
              <p className="font-heading text-lg text-brand-primary">Yoga Marketplace</p>
              <p className="truncate text-xs text-brand-muted">Owner · {ownerName}</p>
            </div>
            <Button variant="text" onClick={onSignOut} sx={{ minHeight: 40, flexShrink: 0 }}>
              Sign out
            </Button>
          </div>
          <nav aria-label="Owner" className="flex gap-1 overflow-x-auto px-4 pb-3">
            {navItems.map((item) => {
              const active = isNavActive(pathname, item.to, item.end);
              return (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.end}
                  aria-current={active ? "page" : undefined}
                  className={cn(
                    "inline-flex shrink-0 items-center gap-2 rounded-full px-3 py-2 text-sm font-medium",
                    active ? "bg-brand-surface text-brand-primary" : "text-brand-muted",
                  )}
                >
                  <NavIcon icon={item.icon} count={countFor(item.to)} className="size-4" />
                  {item.label}
                </NavLink>
              );
            })}
          </nav>
        </header>

        <Suspense fallback={<PageLoader />}>
          <Outlet />
        </Suspense>
      </div>
    </div>
  );
}

import { Suspense } from "react";
import { NavLink, Outlet, useLocation } from "react-router-dom";
import { CalendarDays, Home, User } from "lucide-react";
import { PageLoader } from "@/components/common/page-loader";
import { routes } from "@/constants/routes";
import { cn } from "@/utils/cn";

const navItems = [
  { to: routes.home, label: "Home", icon: Home },
  { to: routes.bookings, label: "Bookings", icon: CalendarDays },
  { to: routes.profile, label: "Profile", icon: User },
];

function isNavActive(pathname: string, to: string) {
  if (to === routes.home) {
    return pathname === routes.home || pathname.startsWith(`${routes.instructors}/`);
  }
  return pathname === to;
}

function itemClass(active: boolean, compact: boolean) {
  return cn(
    compact
      ? "flex flex-col items-center gap-1 rounded-2xl py-1 text-[11px] font-medium"
      : "inline-flex items-center justify-center gap-2 rounded-full px-3 py-2 text-sm font-medium",
    active ? "text-brand-primary" : "text-brand-muted",
  );
}

export function CustomerLayout() {
  const { pathname } = useLocation();

  return (
    <div className="min-h-svh bg-brand-background">
      <header className="sticky top-0 z-20 hidden border-b border-brand-border bg-brand-background/95 px-8 py-3 backdrop-blur md:block">
        <div className="mx-auto flex max-w-5xl items-center justify-between">
          <p className="font-heading text-lg text-brand-primary">Yoga Marketplace</p>
          <nav aria-label="Customer" className="flex items-center gap-2">
            {navItems.map((item) => {
              const Icon = item.icon;
              const active = isNavActive(pathname, item.to);
              return (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.to === routes.home}
                  aria-current={pathname === item.to ? "page" : undefined}
                  className={itemClass(active, false)}
                >
                  <Icon className="size-5" aria-hidden="true" />
                  {item.label}
                </NavLink>
              );
            })}
          </nav>
        </div>
      </header>

      <div className="pb-[calc(4.75rem+env(safe-area-inset-bottom))] md:pb-0">
        <Suspense fallback={<PageLoader />}>
          <Outlet />
        </Suspense>
      </div>

      <nav
        aria-label="Customer"
        className="fixed inset-x-0 bottom-0 z-20 border-t border-brand-border bg-brand-surface/95 px-2 pt-2 pb-[max(0.5rem,env(safe-area-inset-bottom))] backdrop-blur md:hidden"
      >
        <div className="mx-auto grid max-w-md grid-cols-3">
          {navItems.map((item) => {
            const Icon = item.icon;
            const active = isNavActive(pathname, item.to);
            return (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === routes.home}
                aria-current={pathname === item.to ? "page" : undefined}
                className={itemClass(active, true)}
              >
                <Icon className="size-5" aria-hidden="true" />
                {item.label}
              </NavLink>
            );
          })}
        </div>
      </nav>
    </div>
  );
}

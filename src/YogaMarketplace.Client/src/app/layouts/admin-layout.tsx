import { Suspense, useState, type ComponentType } from "react";
import { NavLink, Outlet, useLocation } from "react-router-dom";
import { Badge, Button, Drawer } from "@mui/material";
import { Menu } from "lucide-react";
import { PageLoader } from "@/components/common/page-loader";
import { adminNav, adminNavGroups, type AdminNavBadge, type AdminNavItem } from "@/app/layouts/admin-nav";
import { useAuth } from "@/features/auth/hooks/use-auth";
import { useSignOut } from "@/features/auth/hooks/use-sign-out";
import { useAdminErrorSummary } from "@/features/admin/hooks/use-admin";
import { displayName } from "@/features/admin/utils";
import { cn } from "@/utils/cn";

const badgeMax = 99;

function isNavActive(pathname: string, to: string, end?: boolean) {
  if (end) return pathname === to;
  return pathname === to || pathname.startsWith(`${to}/`);
}

function NavIcon({
  icon: Icon,
  count,
  className,
}: {
  icon: ComponentType<{ className?: string }>;
  count: number;
  className: string;
}) {
  return (
    <>
      <Badge badgeContent={count} color="error" max={badgeMax}>
        <Icon className={className} aria-hidden="true" />
      </Badge>
      {count > 0 ? <span className="sr-only">{`${count} open`}</span> : null}
    </>
  );
}

function OwnerNav({
  pathname,
  openGroup,
  onToggle,
  onNavigate,
  countFor,
}: {
  pathname: string;
  openGroup: string | null;
  onToggle: (group: string) => void;
  onNavigate: () => void;
  countFor: (badge?: AdminNavBadge) => number;
}) {
  return (
    <nav aria-label="Owner" className="flex-1 space-y-1 px-3">
      {adminNavGroups(adminNav).map((group) => {
        const lone = group.items.length === 1 && group.items[0].label === group.name ? group.items[0] : null;
        if (lone) {
          return (
            <NavItemLink key={lone.to} item={lone} pathname={pathname} count={countFor(lone.badge)} onNavigate={onNavigate} />
          );
        }
        const open = openGroup === group.name;
        return (
          <div key={group.name}>
            <button
              type="button"
              aria-expanded={open}
              onClick={() => onToggle(group.name)}
              className="flex w-full items-center rounded-2xl px-3 py-2 text-left text-xs font-semibold tracking-[0.14em] text-brand-muted uppercase"
            >
              {group.name}
            </button>
            {open
              ? group.items.map((item) => (
                  <NavItemLink
                    key={item.to}
                    item={item}
                    pathname={pathname}
                    count={countFor(item.badge)}
                    onNavigate={onNavigate}
                  />
                ))
              : null}
          </div>
        );
      })}
    </nav>
  );
}

function NavItemLink({
  item,
  pathname,
  count,
  onNavigate,
}: {
  item: AdminNavItem;
  pathname: string;
  count: number;
  onNavigate: () => void;
}) {
  const active = isNavActive(pathname, item.to, item.end);
  return (
    <NavLink
      to={item.to}
      end={item.end}
      aria-current={active ? "page" : undefined}
      onClick={onNavigate}
      className={cn(
        "flex items-center gap-3 rounded-2xl px-3 py-2.5 text-sm font-medium",
        active ? "bg-brand-background text-brand-primary" : "text-brand-muted",
      )}
    >
      <NavIcon icon={item.icon} count={count} className="size-4 shrink-0" />
      {item.label}
    </NavLink>
  );
}

export function AdminLayout() {
  const { pathname } = useLocation();
  const { user } = useAuth();
  const onSignOut = useSignOut();
  const ownerName = displayName(user?.name, user?.phone ?? "");
  const openErrors = useAdminErrorSummary().data?.unacknowledged ?? 0;
  const activeGroup = adminNav.find((item) => isNavActive(pathname, item.to, item.end))?.group ?? null;
  const [manual, setManual] = useState<{ path: string; group: string | null } | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const openGroup = manual?.path === pathname ? manual.group : activeGroup;

  function toggleGroup(group: string) {
    setManual({ path: pathname, group: openGroup === group ? null : group });
  }

  const countFor = (badge?: AdminNavBadge) => (badge === "errors" ? openErrors : 0);
  const navProps = {
    pathname,
    openGroup,
    onToggle: toggleGroup,
    onNavigate: () => setDrawerOpen(false),
    countFor,
  };

  return (
    <div className="min-h-svh bg-brand-background lg:flex">
      <aside className="hidden lg:flex lg:w-64 lg:shrink-0 lg:flex-col lg:border-r lg:border-brand-border lg:bg-brand-surface">
        <div className="px-5 py-6">
          <p className="font-heading text-lg text-brand-primary">Yoga Marketplace</p>
          <p className="mt-0.5 text-xs font-medium tracking-[0.16em] text-brand-muted uppercase">Owner</p>
        </div>
        <OwnerNav {...navProps} />
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
            <Button
              variant="text"
              aria-label="Open menu"
              onClick={() => setDrawerOpen(true)}
              sx={{ minWidth: 40, minHeight: 40, px: 1 }}
            >
              <Menu className="size-5" aria-hidden="true" />
            </Button>
            <div className="min-w-0 flex-1">
              <p className="font-heading text-lg text-brand-primary">Yoga Marketplace</p>
              <p className="truncate text-xs text-brand-muted">Owner · {ownerName}</p>
            </div>
            <Button variant="text" onClick={onSignOut} sx={{ minHeight: 40, flexShrink: 0 }}>
              Sign out
            </Button>
          </div>
        </header>
        <Drawer anchor="left" open={drawerOpen} onClose={() => setDrawerOpen(false)}>
          <div className="flex h-full w-72 flex-col bg-brand-surface py-4">
            <OwnerNav {...navProps} />
          </div>
        </Drawer>

        <Suspense fallback={<PageLoader />}>
          <Outlet />
        </Suspense>
      </div>
    </div>
  );
}

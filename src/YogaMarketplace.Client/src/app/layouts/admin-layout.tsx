import { Suspense, useState, type ComponentType } from "react";
import { NavLink, Outlet, useLocation } from "react-router-dom";
import { Badge, Box, Button, Divider, Drawer } from "@mui/material";
import { ChevronDown, Menu } from "lucide-react";
import { PageLoader } from "@/components/common/page-loader";
import { adminGroupIcon, adminNav, adminNavGroups, type AdminNavBadge, type AdminNavItem } from "@/app/layouts/admin-nav";
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
      <Badge badgeContent={count} color="error" max={badgeMax} invisible={count === 0}>
        <Icon className={className} aria-hidden="true" />
      </Badge>
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
    <nav aria-label="Owner" className="flex-1 px-3">
      {adminNavGroups(adminNav).map((group, index) => {
        const lone = group.items.length === 1 && group.items[0].label === group.name ? group.items[0] : null;
        return (
          <div key={group.name}>
            {index > 0 ? <Divider sx={{ mt: 1.5 }} /> : null}
            <div className={index > 0 ? "pt-2" : undefined}>
              {lone ? (
                <NavItemLink item={lone} pathname={pathname} count={countFor(lone.badge)} onNavigate={onNavigate} />
              ) : (
                <NavGroup
                  name={group.name}
                  items={group.items}
                  pathname={pathname}
                  open={openGroup === group.name}
                  onToggle={onToggle}
                  onNavigate={onNavigate}
                  countFor={countFor}
                />
              )}
            </div>
          </div>
        );
      })}
    </nav>
  );
}

function NavGroup({
  name,
  items,
  pathname,
  open,
  onToggle,
  onNavigate,
  countFor,
}: {
  name: string;
  items: AdminNavItem[];
  pathname: string;
  open: boolean;
  onToggle: (group: string) => void;
  onNavigate: () => void;
  countFor: (badge?: AdminNavBadge) => number;
}) {
  const Icon = adminGroupIcon[name];
  const containsActive = items.some((item) => isNavActive(pathname, item.to, item.end));
  const collapsedCount = open ? 0 : items.reduce((sum, item) => sum + countFor(item.badge), 0);
  return (
    <div>
      <Box
        component="button"
        type="button"
        aria-expanded={open}
        onClick={() => onToggle(name)}
        sx={{
          display: "flex",
          width: "100%",
          alignItems: "center",
          gap: 1.5,
          border: 0,
          borderRadius: 2,
          bgcolor: "transparent",
          px: 1.5,
          py: 1,
          textAlign: "left",
          font: "inherit",
          fontSize: 14,
          fontWeight: 500,
          cursor: "pointer",
          color: containsActive ? "primary.main" : "text.secondary",
        }}
      >
        {Icon ? <NavIcon icon={Icon} count={collapsedCount} className="size-4 shrink-0" /> : null}
        <span className="min-w-0 flex-1">
          {name}
          {collapsedCount > 0 ? <span className="sr-only">{` ${collapsedCount} open`}</span> : null}
        </span>
        <ChevronDown className={cn("size-4 shrink-0 transition-transform", open && "rotate-180")} aria-hidden="true" />
      </Box>
      {open
        ? items.map((item) => (
            <NavItemLink
              key={item.to}
              item={item}
              pathname={pathname}
              count={countFor(item.badge)}
              onNavigate={onNavigate}
              nested
            />
          ))
        : null}
    </div>
  );
}

function NavItemLink({
  item,
  pathname,
  count,
  onNavigate,
  nested = false,
}: {
  item: AdminNavItem;
  pathname: string;
  count: number;
  onNavigate: () => void;
  nested?: boolean;
}) {
  const active = isNavActive(pathname, item.to, item.end);
  return (
    <Box
      component={NavLink}
      to={item.to}
      end={item.end}
      aria-current={active ? "page" : undefined}
      onClick={onNavigate}
      sx={{
        display: "flex",
        alignItems: "center",
        gap: 1.5,
        borderRadius: 2,
        borderLeft: "3px solid",
        borderLeftColor: active ? "primary.main" : "transparent",
        bgcolor: active ? "background.default" : "transparent",
        color: active ? "primary.main" : "text.secondary",
        pl: nested ? 3 : 1.5,
        pr: 1.5,
        py: 1,
        fontSize: nested ? 13 : 14,
        fontWeight: 500,
        textDecoration: "none",
      }}
    >
      <NavIcon icon={item.icon} count={count} className="size-4 shrink-0" />
      {item.label}
      {count > 0 ? <span className="sr-only">{` ${count} open`}</span> : null}
    </Box>
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

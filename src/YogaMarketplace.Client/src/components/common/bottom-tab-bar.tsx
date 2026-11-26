import { Link } from "react-router-dom";
import type { NavItem } from "@/app/layouts/nav-config";
import { isNavItemActive } from "@/app/layouts/nav-config";
import { cn } from "@/utils/cn";

const mobileColumns: Record<number, string> = {
  2: "grid-cols-2",
  3: "grid-cols-3",
  4: "grid-cols-4",
  5: "grid-cols-5",
};

type BottomTabBarProps = {
  items: NavItem[];
  pathname: string;
  ariaLabel: string;
};

function itemClass(active: boolean, compact: boolean) {
  return cn(
    compact
      ? "flex flex-col items-center gap-1 rounded-2xl py-1 text-[11px] font-medium"
      : "inline-flex items-center justify-center gap-2 rounded-full px-3 py-2 text-sm font-medium",
    active ? "text-brand-primary" : "text-brand-muted",
  );
}

function NavItemLink({
  item,
  pathname,
  compact,
}: {
  item: NavItem;
  pathname: string;
  compact: boolean;
}) {
  const Icon = item.icon;
  const active = isNavItemActive(pathname, item);
  return (
    <Link to={item.to} aria-current={active ? "page" : undefined} className={itemClass(active, compact)}>
      <Icon className="size-5" aria-hidden="true" />
      {item.label}
    </Link>
  );
}

/** Desktop header links — same items and active rules as the mobile bar. */
export function RoleNavLinks({
  items,
  pathname,
  ariaLabel,
}: {
  items: NavItem[];
  pathname: string;
  ariaLabel: string;
}) {
  return (
    <nav aria-label={ariaLabel} className="flex items-center gap-2">
      {items.map((item) => (
        <NavItemLink key={item.to} item={item} pathname={pathname} compact={false} />
      ))}
    </nav>
  );
}

/** Fixed mobile bottom tabs. Same look and safe-area inset as the previous layout bars. */
export function BottomTabBar({ items, pathname, ariaLabel }: BottomTabBarProps) {
  const columns = mobileColumns[items.length] ?? "grid-cols-5";

  return (
    <nav
      data-testid="bottom-tab-bar"
      aria-label={ariaLabel}
      className="fixed inset-x-0 bottom-0 z-20 border-t border-brand-border bg-brand-surface/95 px-2 pt-2 pb-[max(0.5rem,env(safe-area-inset-bottom))] backdrop-blur md:hidden"
    >
      <div className={cn("mx-auto grid max-w-md", columns)}>
        {items.map((item) => (
          <NavItemLink key={item.to} item={item} pathname={pathname} compact />
        ))}
      </div>
    </nav>
  );
}

import { useEffect } from "react";
import { IconButton, Drawer as MuiDrawer } from "@mui/material";
import { Menu, X } from "lucide-react";
import { Link, useLocation } from "react-router-dom";
import type { DrawerItem } from "@/app/layouts/nav-config";
import { isNavItemActive } from "@/app/layouts/nav-config";
import { useSignOut } from "@/features/auth/hooks/use-sign-out";
import { cn } from "@/utils/cn";

type AppSideDrawerProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  items: DrawerItem[];
  brand: string;
};

function drawerItemClass(active: boolean) {
  return cn(
    "flex w-full items-center gap-3 rounded-2xl px-3 py-3 text-left text-sm font-medium",
    active ? "bg-brand-primary/10 text-brand-primary" : "text-brand-muted hover:bg-brand-border/40",
  );
}

/** Role menu: Settings / Terms / Logout (or Terms only for guests). */
export function AppSideDrawer({ open, onOpenChange, items, brand }: AppSideDrawerProps) {
  const { pathname } = useLocation();
  const signOut = useSignOut();

  useEffect(() => {
    onOpenChange(false);
  }, [pathname, onOpenChange]);

  return (
    <MuiDrawer
      anchor="right"
      open={open}
      onClose={() => onOpenChange(false)}
      slotProps={{
        paper: {
          id: "app-side-drawer",
          className: "w-[min(20rem,100vw)] bg-brand-surface",
        },
      }}
    >
      <div data-testid="app-side-drawer" className="flex h-full flex-col">
      <div className="flex items-center justify-between border-b border-brand-border px-4 py-3">
        <p className="font-heading text-base text-brand-primary">{brand}</p>
        <IconButton aria-label="Close menu" onClick={() => onOpenChange(false)} size="small">
          <X className="size-5" aria-hidden="true" />
        </IconButton>
      </div>
      <nav aria-label="Menu" className="flex flex-col gap-1 p-3">
        {items.map((item) => {
          if (item.kind === "action") {
            const Icon = item.icon;
            return (
              <button
                key={item.id}
                type="button"
                className={drawerItemClass(false)}
                onClick={() => {
                  onOpenChange(false);
                  signOut();
                }}
              >
                <Icon className="size-5" aria-hidden="true" />
                {item.label}
              </button>
            );
          }

          const Icon = item.icon;
          const active = isNavItemActive(pathname, item);
          return (
            <Link
              key={item.to}
              to={item.to}
              aria-current={active ? "page" : undefined}
              className={drawerItemClass(active)}
              onClick={() => onOpenChange(false)}
            >
              <Icon className="size-5" aria-hidden="true" />
              {item.label}
            </Link>
          );
        })}
      </nav>
      </div>
    </MuiDrawer>
  );
}

export function MenuButton({ open, onClick }: { open: boolean; onClick: () => void }) {
  return (
    <IconButton
      aria-label="Open menu"
      aria-expanded={open}
      aria-controls={open ? "app-side-drawer" : undefined}
      onClick={onClick}
      size="small"
      data-testid="menu-button"
    >
      <Menu className="size-5" aria-hidden="true" />
    </IconButton>
  );
}

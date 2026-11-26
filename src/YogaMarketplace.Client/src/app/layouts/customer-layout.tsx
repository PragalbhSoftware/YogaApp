import { Suspense, useState } from "react";
import { Outlet, useLocation } from "react-router-dom";
import {
  customerDrawerItems,
  customerNavItems,
  guestDrawerItems,
  guestNavItems,
  shouldShowBottomTabBar,
  tabBarContentPaddingClass,
} from "@/app/layouts/nav-config";
import { AppSideDrawer, MenuButton } from "@/components/common/app-side-drawer";
import { BottomTabBar, RoleNavLinks } from "@/components/common/bottom-tab-bar";
import { PageLoader } from "@/components/common/page-loader";
import { SiteFooter } from "@/components/common/site-footer";
import { useAuth } from "@/features/auth/hooks/use-auth";
import { CitySwitcher } from "@/features/marketplace/components/city-switcher";
import { CustomerCityPicker } from "@/features/marketplace/components/customer-city-picker";
import { usePreferredCitySync } from "@/features/marketplace/hooks/use-preferred-city-sync";
import { useProfileBadges } from "@/features/profile/hooks/use-profile";
import { cn } from "@/utils/cn";

const brand = "Yoga Marketplace";

export function CustomerLayout() {
  const { pathname } = useLocation();
  const { isAuthenticated } = useAuth();
  const navItems = isAuthenticated ? customerNavItems : guestNavItems;
  const drawerItems = isAuthenticated ? customerDrawerItems : guestDrawerItems;
  const showTabBar = shouldShowBottomTabBar(pathname);
  const [menuOpen, setMenuOpen] = useState(false);
  const badges = useProfileBadges();
  const badgeCounts = { bookings: badges.data?.bookings ?? 0 };
  usePreferredCitySync();

  return (
    <div className="min-h-svh bg-brand-background">
      <header className="sticky top-0 z-20 hidden border-b border-brand-border bg-brand-background/95 px-8 py-3 backdrop-blur md:block">
        <div className="mx-auto flex max-w-5xl items-center justify-between gap-4">
          <p className="font-heading text-lg text-brand-primary">{brand}</p>
          <div className="flex items-center gap-2">
            <CitySwitcher />
            <RoleNavLinks
              items={navItems}
              pathname={pathname}
              ariaLabel="Customer"
              badgeCounts={badgeCounts}
            />
            <MenuButton open={menuOpen} onClick={() => setMenuOpen(true)} />
          </div>
        </div>
      </header>

      <header className="sticky top-0 z-20 flex items-center justify-between border-b border-brand-border bg-brand-background/95 px-4 py-2 backdrop-blur md:hidden">
        <p className="font-heading text-base text-brand-primary">{brand}</p>
        <div className="flex items-center gap-1">
          <CitySwitcher compact />
          <MenuButton open={menuOpen} onClick={() => setMenuOpen(true)} />
        </div>
      </header>

      <div data-testid="layout-content" className={cn(showTabBar && tabBarContentPaddingClass)}>
        <CustomerCityPicker />
        <Suspense fallback={<PageLoader />}>
          <Outlet />
        </Suspense>
        <SiteFooter />
      </div>

      {showTabBar ? (
        <BottomTabBar
          items={navItems}
          pathname={pathname}
          ariaLabel="Customer"
          badgeCounts={badgeCounts}
        />
      ) : null}

      <AppSideDrawer
        open={menuOpen}
        onOpenChange={setMenuOpen}
        items={drawerItems}
        brand={brand}
      />
    </div>
  );
}

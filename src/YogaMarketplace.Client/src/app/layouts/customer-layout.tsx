import { Suspense } from "react";
import { Outlet, useLocation } from "react-router-dom";
import {
  customerNavItems,
  guestNavItems,
  shouldShowBottomTabBar,
  tabBarContentPaddingClass,
} from "@/app/layouts/nav-config";
import { PageLoader } from "@/components/common/page-loader";
import { BottomTabBar, RoleNavLinks } from "@/components/common/bottom-tab-bar";
import { SiteFooter } from "@/components/common/site-footer";
import { useAuth } from "@/features/auth/hooks/use-auth";
import { CitySwitcher } from "@/features/marketplace/components/city-switcher";
import { CustomerCityPicker } from "@/features/marketplace/components/customer-city-picker";
import { usePreferredCitySync } from "@/features/marketplace/hooks/use-preferred-city-sync";
import { cn } from "@/utils/cn";

export function CustomerLayout() {
  const { pathname } = useLocation();
  const { isAuthenticated } = useAuth();
  const navItems = isAuthenticated ? customerNavItems : guestNavItems;
  const showTabBar = shouldShowBottomTabBar(pathname);
  usePreferredCitySync();

  return (
    <div className="min-h-svh bg-brand-background">
      <header className="sticky top-0 z-20 hidden border-b border-brand-border bg-brand-background/95 px-8 py-3 backdrop-blur md:block">
        <div className="mx-auto flex max-w-5xl items-center justify-between gap-4">
          <p className="font-heading text-lg text-brand-primary">Yoga Marketplace</p>
          <div className="flex items-center gap-2">
            <CitySwitcher />
            <RoleNavLinks items={navItems} pathname={pathname} ariaLabel="Customer" />
          </div>
        </div>
      </header>

      <header className="sticky top-0 z-20 flex items-center justify-between border-b border-brand-border bg-brand-background/95 px-4 py-2 backdrop-blur md:hidden">
        <p className="font-heading text-base text-brand-primary">Yoga Marketplace</p>
        <CitySwitcher compact />
      </header>

      <div
        data-testid="layout-content"
        className={cn(showTabBar && tabBarContentPaddingClass)}
      >
        <CustomerCityPicker />
        <Suspense fallback={<PageLoader />}>
          <Outlet />
        </Suspense>
        <SiteFooter />
      </div>

      {showTabBar ? (
        <BottomTabBar items={navItems} pathname={pathname} ariaLabel="Customer" />
      ) : null}
    </div>
  );
}

import { Suspense } from "react";
import { Outlet, useLocation } from "react-router-dom";
import {
  filterRegisteredNavItems,
  instructorNavItems,
  shouldShowBottomTabBar,
  tabBarContentPaddingClass,
} from "@/app/layouts/nav-config";
import { PageLoader } from "@/components/common/page-loader";
import { BottomTabBar, RoleNavLinks } from "@/components/common/bottom-tab-bar";
import { SiteFooter } from "@/components/common/site-footer";
import { cn } from "@/utils/cn";

export function InstructorLayout() {
  const { pathname } = useLocation();
  const navItems = filterRegisteredNavItems(instructorNavItems);
  const showTabBar = shouldShowBottomTabBar(pathname);

  return (
    <div className="min-h-svh bg-brand-background">
      <header className="sticky top-0 z-20 hidden border-b border-brand-border bg-brand-background/95 px-8 py-3 backdrop-blur md:block">
        <div className="mx-auto flex max-w-5xl items-center justify-between">
          <p className="font-heading text-lg text-brand-primary">Instructor</p>
          <RoleNavLinks items={navItems} pathname={pathname} ariaLabel="Instructor" />
        </div>
      </header>

      <div
        data-testid="layout-content"
        className={cn(showTabBar && tabBarContentPaddingClass)}
      >
        <Suspense fallback={<PageLoader />}>
          <Outlet />
        </Suspense>
        <SiteFooter withPacks={false} />
      </div>

      {showTabBar ? (
        <BottomTabBar items={navItems} pathname={pathname} ariaLabel="Instructor" />
      ) : null}
    </div>
  );
}

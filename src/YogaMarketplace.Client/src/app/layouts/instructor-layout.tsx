import { Suspense, useState } from "react";
import { Outlet, useLocation } from "react-router-dom";
import {
  instructorDrawerItems,
  instructorNavItems,
  shouldShowBottomTabBar,
  tabBarContentPaddingClass,
} from "@/app/layouts/nav-config";
import { AppSideDrawer, MenuButton } from "@/components/common/app-side-drawer";
import { BottomTabBar, RoleNavLinks } from "@/components/common/bottom-tab-bar";
import { PageLoader } from "@/components/common/page-loader";
import { SiteFooter } from "@/components/common/site-footer";
import { useProfileBadges } from "@/features/profile/hooks/use-profile";
import { cn } from "@/utils/cn";

const brand = "Instructor";

export function InstructorLayout() {
  const { pathname } = useLocation();
  const navItems = instructorNavItems;
  const showTabBar = shouldShowBottomTabBar(pathname);
  const [menuOpen, setMenuOpen] = useState(false);
  const badges = useProfileBadges();
  const badgeCounts = { requests: badges.data?.requests ?? 0 };

  return (
    <div className="min-h-svh bg-brand-background">
      <header className="sticky top-0 z-20 hidden border-b border-brand-border bg-brand-background/95 px-8 py-3 backdrop-blur md:block">
        <div className="mx-auto flex max-w-5xl items-center justify-between">
          <p className="font-heading text-lg text-brand-primary">{brand}</p>
          <div className="flex items-center gap-2">
            <RoleNavLinks
              items={navItems}
              pathname={pathname}
              ariaLabel="Instructor"
              badgeCounts={badgeCounts}
            />
            <MenuButton open={menuOpen} onClick={() => setMenuOpen(true)} />
          </div>
        </div>
      </header>

      <header className="sticky top-0 z-20 flex items-center justify-between border-b border-brand-border bg-brand-background/95 px-4 py-2 backdrop-blur md:hidden">
        <p className="font-heading text-base text-brand-primary">{brand}</p>
        <MenuButton open={menuOpen} onClick={() => setMenuOpen(true)} />
      </header>

      <div data-testid="layout-content" className={cn(showTabBar && tabBarContentPaddingClass)}>
        <Suspense fallback={<PageLoader />}>
          <Outlet />
        </Suspense>
        <SiteFooter withPacks={false} />
      </div>

      {showTabBar ? (
        <BottomTabBar
          items={navItems}
          pathname={pathname}
          ariaLabel="Instructor"
          badgeCounts={badgeCounts}
        />
      ) : null}

      <AppSideDrawer
        open={menuOpen}
        onOpenChange={setMenuOpen}
        items={instructorDrawerItems}
        brand={brand}
      />
    </div>
  );
}

import { describe, expect, it } from "vitest";
import {
  customerNavItems,
  guestNavItems,
  instructorNavItems,
  isNavItemActive,
  shouldShowBottomTabBar,
} from "@/app/layouts/nav-config";
import { routes } from "@/constants/routes";
import routerSource from "@/app/router/app-router.tsx?raw";

function routeKeyForPath(path: string): string | undefined {
  return (Object.entries(routes) as [string, string][]).find(([, value]) => value === path)?.[0];
}

describe("nav-config", () => {
  it("lists every guest tab label and route", () => {
    expect(guestNavItems.map((item) => [item.label, item.to])).toEqual([
      ["Home", routes.home],
      ["Sign in", routes.login],
    ]);
  });

  it("lists every customer tab label and route", () => {
    expect(customerNavItems.map((item) => [item.label, item.to])).toEqual([
      ["Home", routes.home],
      ["Bookings", routes.bookings],
      ["Credits", routes.packs],
      ["Profile", routes.profile],
    ]);
  });

  it("lists every instructor tab label and route", () => {
    expect(instructorNavItems.map((item) => [item.label, item.to])).toEqual([
      ["Schedule", routes.instructor],
      ["Requests", routes.instructorBookings],
      ["Earnings", routes.instructorEarnings],
      ["Services", routes.instructorServices],
      ["Profile", routes.instructorProfile],
    ]);
  });

  it("points every tab to a route registered in app-router", () => {
    for (const item of [...guestNavItems, ...customerNavItems, ...instructorNavItems]) {
      const key = routeKeyForPath(item.to);
      expect(key, `no routes.* entry for ${item.to}`).toBeDefined();
      expect(routerSource).toContain(`path={routes.${key}}`);
    }
  });

  it("hides the bar on booking checkout and shows it elsewhere", () => {
    expect(shouldShowBottomTabBar("/book/provider-1/slot-2")).toBe(false);
    expect(shouldShowBottomTabBar("/book")).toBe(false);
    expect(shouldShowBottomTabBar(routes.bookings)).toBe(true);
    expect(shouldShowBottomTabBar(routes.home)).toBe(true);
  });

  it("highlights deep routes on the parent tab", () => {
    const bookings = customerNavItems.find((item) => item.to === routes.bookings)!;
    const home = customerNavItems.find((item) => item.to === routes.home)!;
    const schedule = instructorNavItems.find((item) => item.to === routes.instructor)!;
    const requests = instructorNavItems.find((item) => item.to === routes.instructorBookings)!;

    expect(isNavItemActive("/bookings/123", bookings)).toBe(true);
    expect(isNavItemActive(`${routes.instructors}/abc`, home)).toBe(true);
    expect(isNavItemActive(routes.home, home)).toBe(true);
    expect(isNavItemActive(routes.instructorBookings, schedule)).toBe(false);
    expect(isNavItemActive(`${routes.instructorBookings}/x`, requests)).toBe(true);
  });
});

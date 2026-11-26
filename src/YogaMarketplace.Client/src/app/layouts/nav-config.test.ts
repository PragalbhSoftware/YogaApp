import { describe, expect, it } from "vitest";
import {
  customerNavItems,
  filterRegisteredNavItems,
  guestNavItems,
  instructorNavItems,
  isNavItemActive,
  shouldShowBottomTabBar,
} from "@/app/layouts/nav-config";
import { routes } from "@/constants/routes";

describe("nav-config", () => {
  it("lists every guest tab label and route", () => {
    expect(filterRegisteredNavItems(guestNavItems).map((item) => [item.label, item.to])).toEqual([
      ["Home", routes.home],
      ["Sign in", routes.login],
    ]);
  });

  it("lists every customer tab label and route", () => {
    expect(filterRegisteredNavItems(customerNavItems).map((item) => [item.label, item.to])).toEqual([
      ["Home", routes.home],
      ["Explore", routes.explore],
      ["Bookings", routes.bookings],
      ["Credits", routes.packs],
      ["Profile", routes.profile],
    ]);
  });

  it("lists every instructor tab label and route", () => {
    expect(filterRegisteredNavItems(instructorNavItems).map((item) => [item.label, item.to])).toEqual([
      ["Schedule", routes.instructor],
      ["Requests", routes.instructorBookings],
      ["Earnings", routes.instructorEarnings],
      ["Services", routes.instructorServices],
      ["Profile", routes.instructorProfile],
    ]);
  });

  it("hides the bar on booking checkout and shows it elsewhere", () => {
    expect(shouldShowBottomTabBar("/book/provider-1/slot-2")).toBe(false);
    expect(shouldShowBottomTabBar("/book")).toBe(false);
    expect(shouldShowBottomTabBar(routes.bookings)).toBe(true);
    expect(shouldShowBottomTabBar(routes.home)).toBe(true);
  });

  it("highlights deep routes on the parent tab", () => {
    const bookings = customerNavItems.find((item) => item.to === routes.bookings)!;
    const explore = customerNavItems.find((item) => item.to === routes.explore)!;
    const home = customerNavItems.find((item) => item.to === routes.home)!;
    const schedule = instructorNavItems.find((item) => item.to === routes.instructor)!;
    const requests = instructorNavItems.find((item) => item.to === routes.instructorBookings)!;

    expect(isNavItemActive("/bookings/123", bookings)).toBe(true);
    expect(isNavItemActive(`${routes.instructors}/abc`, explore)).toBe(true);
    expect(isNavItemActive(`${routes.instructors}/abc`, home)).toBe(false);
    expect(isNavItemActive(routes.instructorBookings, schedule)).toBe(false);
    expect(isNavItemActive(`${routes.instructorBookings}/x`, requests)).toBe(true);
  });
});

import { fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CustomerLayout } from "@/app/layouts/customer-layout";
import {
  customerNavItems,
  filterRegisteredNavItems,
  guestNavItems,
  tabBarContentPaddingClass,
} from "@/app/layouts/nav-config";
import { routes } from "@/constants/routes";
import { useAuthStore } from "@/stores/auth-store";

vi.mock("@/features/marketplace/components/customer-city-picker", () => ({ CustomerCityPicker: () => null }));
vi.mock("@/features/marketplace/components/city-switcher", () => ({ CitySwitcher: () => null }));
vi.mock("@/features/marketplace/hooks/use-preferred-city-sync", () => ({ usePreferredCitySync: () => undefined }));

function Pathname() {
  const { pathname } = useLocation();
  return <p>{pathname}</p>;
}

function renderAt(path: string, signedIn = false) {
  if (signedIn) {
    useAuthStore
      .getState()
      .signIn("token", { id: "u1", name: "Test", phone: "+919000000000", gender: null, role: "Customer" });
  }

  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route element={<CustomerLayout />}>
          <Route path={routes.home} element={<Pathname />} />
          <Route path={routes.explore} element={<Pathname />} />
          <Route path={routes.bookings} element={<Pathname />} />
          <Route path="/bookings/:id" element={<Pathname />} />
          <Route path={routes.packs} element={<Pathname />} />
          <Route path={routes.profile} element={<Pathname />} />
          <Route path={routes.book} element={<Pathname />} />
          <Route path={routes.teacher} element={<Pathname />} />
        </Route>
        <Route path={routes.login} element={<Pathname />} />
      </Routes>
    </MemoryRouter>,
  );
}

function bottomBar() {
  return screen.getByTestId("bottom-tab-bar");
}

function tabLabelsAndHrefs(nav: HTMLElement) {
  return within(nav)
    .getAllByRole("link")
    .map((link) => [link.textContent, link.getAttribute("href")]);
}

describe("CustomerLayout navigation", () => {
  beforeEach(() => useAuthStore.getState().signOut());

  it("shows guest tabs matching nav-config", () => {
    renderAt(routes.home);
    const expected = filterRegisteredNavItems(guestNavItems).map((item) => [item.label, item.to]);
    expect(tabLabelsAndHrefs(bottomBar())).toEqual(expected);
    for (const nav of screen.getAllByRole("navigation", { name: "Customer" })) {
      expect(tabLabelsAndHrefs(nav)).toEqual(expected);
    }
  });

  it("shows customer tabs matching nav-config", () => {
    renderAt(routes.home, true);
    const expected = filterRegisteredNavItems(customerNavItems).map((item) => [item.label, item.to]);
    expect(tabLabelsAndHrefs(bottomBar())).toEqual(expected);
    for (const nav of screen.getAllByRole("navigation", { name: "Customer" })) {
      expect(tabLabelsAndHrefs(nav)).toEqual(expected);
    }
  });

  it("hides the bar on /book/:id and shows it again after leaving", () => {
    renderAt("/book/provider-1/slot-2", true);
    expect(screen.queryByTestId("bottom-tab-bar")).not.toBeInTheDocument();
    expect(screen.getByTestId("layout-content").className).not.toContain("safe-area-inset-bottom");

    fireEvent.click(screen.getAllByRole("link", { name: "Home" })[0]);
    expect(bottomBar()).toBeInTheDocument();
    expect(screen.getByTestId("layout-content").className).toContain(tabBarContentPaddingClass);
  });

  it("highlights Bookings on a deep bookings path", () => {
    renderAt("/bookings/123", true);
    const bookings = within(bottomBar()).getByRole("link", { name: "Bookings" });
    expect(bookings).toHaveAttribute("aria-current", "page");
  });

  it("highlights Explore on teacher detail paths", () => {
    renderAt("/teachers/teacher-1", true);
    expect(within(bottomBar()).getByRole("link", { name: "Explore" })).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(within(bottomBar()).getByRole("link", { name: "Home" })).not.toHaveAttribute(
      "aria-current",
    );
  });

  it("navigates when a tab is clicked", () => {
    renderAt(routes.home, true);
    fireEvent.click(within(bottomBar()).getByRole("link", { name: "Credits" }));
    expect(screen.getByText(routes.packs)).toBeInTheDocument();
  });

  it("applies safe-area content padding only when the bar is shown", () => {
    renderAt(routes.home, true);
    expect(screen.getByTestId("layout-content").className).toContain(tabBarContentPaddingClass);
    expect(bottomBar().className).toContain("md:hidden");
  });

  it("shows the footer with packs and the three policy pages", () => {
    renderAt(routes.home);
    const footer = within(screen.getByRole("navigation", { name: "More" }));
    expect(footer.getAllByRole("link").map((link) => link.getAttribute("href"))).toEqual([
      routes.packs,
      routes.refundPolicy,
      routes.terms,
      routes.privacy,
    ]);
  });
});

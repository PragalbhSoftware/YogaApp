import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CustomerLayout } from "@/app/layouts/customer-layout";
import {
  customerDrawerItems,
  customerNavItems,
  guestDrawerItems,
  guestNavItems,
  tabBarContentPaddingClass,
} from "@/app/layouts/nav-config";
import { routes } from "@/constants/routes";
import { useAuthStore } from "@/stores/auth-store";

const badgesState = vi.hoisted(() => ({ bookings: 0, requests: 0 }));
const signOutMock = vi.hoisted(() => vi.fn());

vi.mock("@/features/marketplace/components/customer-city-picker", () => ({ CustomerCityPicker: () => null }));
vi.mock("@/features/marketplace/components/city-switcher", () => ({ CitySwitcher: () => null }));
vi.mock("@/features/marketplace/hooks/use-preferred-city-sync", () => ({ usePreferredCitySync: () => undefined }));
vi.mock("@/features/profile/hooks/use-profile", () => ({
  useProfileBadges: () => ({ data: { bookings: badgesState.bookings, requests: badgesState.requests } }),
  useCustomerProfile: () => ({ data: undefined, isSuccess: false }),
}));
vi.mock("@/features/auth/hooks/use-sign-out", () => ({
  useSignOut: () => signOutMock,
}));

function Pathname() {
  const { pathname } = useLocation();
  return <p data-testid="path">{pathname}</p>;
}

function renderAt(path: string, signedIn = false) {
  if (signedIn) {
    useAuthStore
      .getState()
      .signIn("token", { id: "u1", name: "Test", phone: "+919000000000", gender: null, role: "Customer" });
  }

  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route element={<CustomerLayout />}>
            <Route path={routes.home} element={<Pathname />} />
            <Route path={routes.bookings} element={<Pathname />} />
            <Route path="/bookings/:id" element={<Pathname />} />
            <Route path={routes.packs} element={<Pathname />} />
            <Route path={routes.profile} element={<Pathname />} />
            <Route path={routes.book} element={<Pathname />} />
            <Route path={routes.teacher} element={<Pathname />} />
            <Route path={routes.terms} element={<Pathname />} />
          </Route>
          <Route path={routes.login} element={<Pathname />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function bottomBar() {
  return screen.getByTestId("bottom-tab-bar");
}

function menuButton() {
  return screen.getByTestId("menu-button");
}

function tabLabelsAndHrefs(nav: HTMLElement) {
  return within(nav)
    .getAllByRole("link")
    .map((link) => [link.textContent?.replace(/\d+/g, "").trim(), link.getAttribute("href")]);
}

describe("CustomerLayout navigation", () => {
  beforeEach(() => {
    useAuthStore.getState().signOut();
    badgesState.bookings = 0;
    badgesState.requests = 0;
    signOutMock.mockReset();
  });

  it("shows guest tabs matching nav-config", () => {
    renderAt(routes.home);
    const expected = guestNavItems.map((item) => [item.label, item.to]);
    expect(tabLabelsAndHrefs(bottomBar())).toEqual(expected);
  });

  it("shows customer tabs matching nav-config", () => {
    renderAt(routes.home, true);
    const expected = customerNavItems.map((item) => [item.label, item.to]);
    expect(tabLabelsAndHrefs(bottomBar())).toEqual(expected);
  });

  it("hides the bar on /book/:id while the drawer is open or closed, and restores it after leaving", async () => {
    renderAt("/book/provider-1/slot-2", true);
    expect(screen.queryByTestId("bottom-tab-bar")).not.toBeInTheDocument();
    expect(screen.getByTestId("layout-content").className).not.toContain(tabBarContentPaddingClass);

    fireEvent.click(menuButton());
    expect(screen.getByTestId("app-side-drawer")).toBeInTheDocument();
    expect(screen.queryByTestId("bottom-tab-bar")).not.toBeInTheDocument();

    fireEvent.click(within(screen.getByTestId("app-side-drawer")).getByRole("button", { name: "Close menu" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(screen.getByTestId("path")).toHaveTextContent("/book/provider-1/slot-2");
    expect(screen.queryByTestId("bottom-tab-bar")).not.toBeInTheDocument();

    fireEvent.click(menuButton());
    fireEvent.click(within(screen.getByTestId("app-side-drawer")).getByRole("link", { name: "Settings" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(screen.getByTestId("path")).toHaveTextContent(routes.profile);
    expect(bottomBar()).toBeInTheDocument();
    expect(screen.getByTestId("layout-content").className).toContain(tabBarContentPaddingClass);
  });

  it("highlights Bookings on a deep bookings path", () => {
    renderAt("/bookings/123", true);
    expect(within(bottomBar()).getByRole("link", { name: /Bookings/ })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });

  it("highlights Home on teacher detail paths", () => {
    renderAt("/teachers/teacher-1", true);
    expect(within(bottomBar()).getByRole("link", { name: "Home" })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });

  it("applies safe-area content padding only when the bar is shown", () => {
    renderAt(routes.home, true);
    expect(screen.getByTestId("layout-content").className).toContain(tabBarContentPaddingClass);
    expect(bottomBar().className).toContain("md:hidden");
  });

  it("keeps the drawer closed by default and sets aria-expanded on the menu button", () => {
    renderAt(routes.home, true);
    expect(screen.queryByTestId("app-side-drawer")).not.toBeInTheDocument();
    const menu = menuButton();
    expect(menu).toHaveAttribute("aria-expanded", "false");
    fireEvent.click(menu);
    expect(menu).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByTestId("app-side-drawer")).toBeInTheDocument();
  });

  it("shows signed-in drawer items matching nav-config", () => {
    renderAt(routes.home, true);
    fireEvent.click(menuButton());
    const labels = within(screen.getByTestId("app-side-drawer"))
      .getAllByRole("link")
      .map((link) => link.textContent)
      .concat(
        within(screen.getByTestId("app-side-drawer"))
          .getAllByRole("button")
          .filter((button) => button.textContent === "Logout")
          .map((button) => button.textContent),
      );
    expect(labels).toEqual(customerDrawerItems.map((item) => item.label));
  });

  it("shows guest drawer items matching nav-config", () => {
    renderAt(routes.home);
    fireEvent.click(menuButton());
    expect(
      within(screen.getByTestId("app-side-drawer"))
        .getAllByRole("link")
        .map((link) => link.textContent),
    ).toEqual(guestDrawerItems.map((item) => item.label));
  });

  it("closes the drawer on Esc, link click, route change, and Logout", async () => {
    renderAt(routes.home, true);
    fireEvent.click(menuButton());
    // Backdrop click uses the same onClose path as Escape.
    fireEvent.click(document.querySelector(".MuiBackdrop-root")!);
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());

    fireEvent.click(menuButton());
    fireEvent.click(within(screen.getByTestId("app-side-drawer")).getByRole("link", { name: "Terms" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(screen.getByTestId("path")).toHaveTextContent(routes.terms);

    fireEvent.click(menuButton());
    fireEvent.click(within(screen.getByTestId("app-side-drawer")).getByRole("link", { name: "Settings" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(screen.getByTestId("path")).toHaveTextContent(routes.profile);

    fireEvent.click(menuButton());
    fireEvent.click(within(screen.getByTestId("app-side-drawer")).getByRole("button", { name: "Logout" }));
    expect(signOutMock).toHaveBeenCalled();
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("hides the Bookings badge at 0 and shows it above 0 with a 99+ cap and pending screen-reader name", () => {
    badgesState.bookings = 0;
    const { unmount } = renderAt(routes.home, true);
    expect(within(bottomBar()).getByRole("link", { name: "Bookings" }).textContent).toBe("Bookings");
    unmount();

    badgesState.bookings = 3;
    const mid = renderAt(routes.home, true);
    expect(within(bottomBar()).getByRole("link", { name: "Bookings, 3 pending" }).textContent).toMatch(/3/);
    mid.unmount();

    badgesState.bookings = 120;
    renderAt(routes.home, true);
    expect(within(bottomBar()).getByRole("link", { name: "Bookings, 99+ pending" }).textContent).toMatch(/99\+/);
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

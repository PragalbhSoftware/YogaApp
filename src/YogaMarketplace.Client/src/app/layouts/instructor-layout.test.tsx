import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { InstructorLayout } from "@/app/layouts/instructor-layout";
import {
  instructorDrawerItems,
  instructorNavItems,
  tabBarContentPaddingClass,
} from "@/app/layouts/nav-config";
import { routes } from "@/constants/routes";
import { useAuthStore } from "@/stores/auth-store";

const badgesState = vi.hoisted(() => ({ bookings: 0, requests: 0 }));

vi.mock("@/features/profile/hooks/use-profile", () => ({
  useProfileBadges: () => ({ data: { bookings: badgesState.bookings, requests: badgesState.requests } }),
}));
vi.mock("@/features/auth/hooks/use-sign-out", () => ({
  useSignOut: () => vi.fn(),
}));

function Pathname() {
  const { pathname } = useLocation();
  return <p data-testid="path">{pathname}</p>;
}

function renderAt(path: string) {
  useAuthStore
    .getState()
    .signIn("token", { id: "p1", name: "Instructor", phone: "+919111111111", gender: null, role: "Provider" });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route element={<InstructorLayout />}>
            <Route path={routes.instructor} element={<Pathname />} />
            <Route path={routes.instructorBookings} element={<Pathname />} />
            <Route path={`${routes.instructorBookings}/:id`} element={<Pathname />} />
            <Route path={routes.instructorEarnings} element={<Pathname />} />
            <Route path={routes.instructorServices} element={<Pathname />} />
            <Route path={routes.instructorProfile} element={<Pathname />} />
            <Route path={routes.terms} element={<Pathname />} />
          </Route>
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function bottomBar() {
  return screen.getByTestId("bottom-tab-bar");
}

function tabLabelsAndHrefs(nav: HTMLElement) {
  return within(nav)
    .getAllByRole("link")
    .map((link) => [link.textContent?.replace(/\d+\+?/g, "").trim(), link.getAttribute("href")]);
}

describe("InstructorLayout navigation", () => {
  beforeEach(() => {
    badgesState.bookings = 0;
    badgesState.requests = 0;
    useAuthStore.getState().signOut();
  });

  it("shows instructor tabs matching nav-config", () => {
    renderAt(routes.instructor);
    const expected = instructorNavItems.map((item) => [item.label, item.to]);
    expect(tabLabelsAndHrefs(bottomBar())).toEqual(expected);
  });

  it("highlights Requests on a deep requests path", () => {
    renderAt(`${routes.instructorBookings}/req-1`);
    expect(within(bottomBar()).getByRole("link", { name: /Requests/ })).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(within(bottomBar()).getByRole("link", { name: "Schedule" })).not.toHaveAttribute(
      "aria-current",
    );
  });

  it("shows drawer items matching nav-config and Settings goes to instructor profile", () => {
    renderAt(routes.instructor);
    fireEvent.click(screen.getAllByTestId("menu-button")[0]);
    const labels = within(screen.getByTestId("app-side-drawer"))
      .getAllByRole("link")
      .map((link) => link.textContent)
      .concat(
        within(screen.getByTestId("app-side-drawer"))
          .getAllByRole("button")
          .filter((button) => button.textContent === "Logout")
          .map((button) => button.textContent),
      );
    expect(labels).toEqual(instructorDrawerItems.map((item) => item.label));
    fireEvent.click(within(screen.getByTestId("app-side-drawer")).getByRole("link", { name: "Settings" }));
    expect(screen.getByTestId("path")).toHaveTextContent(routes.instructorProfile);
  });

  it("shows the Requests badge above 0", () => {
    badgesState.requests = 2;
    renderAt(routes.instructor);
    expect(within(bottomBar()).getByRole("link", { name: /Requests/ }).textContent).toMatch(/2/);
  });

  it("applies safe-area content padding when the bar is shown", () => {
    renderAt(routes.instructor);
    expect(screen.getByTestId("layout-content").className).toContain(tabBarContentPaddingClass);
    expect(bottomBar().className).toContain("md:hidden");
  });

  it("shows the footer with the three policy pages and no packs link", () => {
    renderAt(routes.instructor);
    const footer = within(screen.getByRole("navigation", { name: "More" }));
    expect(footer.getAllByRole("link").map((link) => link.getAttribute("href"))).toEqual([
      routes.refundPolicy,
      routes.terms,
      routes.privacy,
    ]);
  });
});

import { fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { InstructorLayout } from "@/app/layouts/instructor-layout";
import { instructorNavItems, tabBarContentPaddingClass } from "@/app/layouts/nav-config";
import { routes } from "@/constants/routes";

function Pathname() {
  const { pathname } = useLocation();
  return <p>{pathname}</p>;
}

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route element={<InstructorLayout />}>
          <Route path={routes.instructor} element={<Pathname />} />
          <Route path={routes.instructorBookings} element={<Pathname />} />
          <Route path={`${routes.instructorBookings}/:id`} element={<Pathname />} />
          <Route path={routes.instructorEarnings} element={<Pathname />} />
          <Route path={routes.instructorServices} element={<Pathname />} />
          <Route path={routes.instructorProfile} element={<Pathname />} />
        </Route>
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

describe("InstructorLayout navigation", () => {
  it("shows instructor tabs matching nav-config", () => {
    renderAt(routes.instructor);
    const expected = instructorNavItems.map((item) => [item.label, item.to]);
    expect(tabLabelsAndHrefs(bottomBar())).toEqual(expected);
    for (const nav of screen.getAllByRole("navigation", { name: "Instructor" })) {
      expect(tabLabelsAndHrefs(nav)).toEqual(expected);
    }
  });

  it("highlights Requests on a deep requests path", () => {
    renderAt(`${routes.instructorBookings}/req-1`);
    expect(within(bottomBar()).getByRole("link", { name: "Requests" })).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(within(bottomBar()).getByRole("link", { name: "Schedule" })).not.toHaveAttribute(
      "aria-current",
    );
  });

  it("navigates when a tab is clicked", () => {
    renderAt(routes.instructor);
    fireEvent.click(within(bottomBar()).getByRole("link", { name: "Earnings" }));
    expect(screen.getByText(routes.instructorEarnings)).toBeInTheDocument();
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

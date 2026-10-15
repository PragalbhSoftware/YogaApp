import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { AdminLayout } from "@/app/layouts/admin-layout";
import { adminNav } from "@/app/layouts/admin-nav";
import { routes } from "@/constants/routes";

vi.mock("@/features/admin/hooks/use-admin", () => ({
  useAdminErrorSummary: () => ({ data: { unacknowledged: 4 } }),
}));

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AdminLayout />
    </MemoryRouter>,
  );
}

describe("AdminLayout", () => {
  it("opens the group for the current route and marks that item", () => {
    renderAt(routes.adminErrors);

    expect(screen.getByRole("button", { name: "Operations" })).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("button", { name: "Finance" })).toHaveAttribute("aria-expanded", "false");
    expect(screen.getByRole("link", { name: /Errors/ })).toHaveAttribute("aria-current", "page");
    expect(screen.getByText("4 open")).toBeInTheDocument();
  });

  it("expands another group from its header", () => {
    renderAt(routes.admin);
    fireEvent.click(screen.getByRole("button", { name: "Locations" }));
    expect(screen.getByRole("button", { name: "Locations" })).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("link", { name: "Cities" })).toHaveAttribute("href", routes.adminCities);
  });

  it("lists every configured page once its group is open", () => {
    renderAt(routes.admin);
    const groups = [...new Set(adminNav.map((item) => item.group))].filter((group) => group !== "Dashboard");
    for (const group of groups) {
      fireEvent.click(screen.getByRole("button", { name: new RegExp(group) }));
      for (const item of adminNav.filter((entry) => entry.group === group)) {
        const link = screen.getAllByRole("link").find((element) => element.getAttribute("href") === item.to);
        expect(link).toBeTruthy();
      }
    }
    expect(screen.getByRole("link", { name: "Dashboard" })).toHaveAttribute("href", routes.admin);
  });

  it("shows a child badge on the group while it is collapsed", () => {
    renderAt(routes.admin);
    expect(screen.getByRole("button", { name: /Operations/ })).toHaveAttribute("aria-expanded", "false");
    expect(screen.getByRole("button", { name: /Operations/ })).toHaveTextContent("4 open");
  });

  it("opens the same menu from the header button", () => {
    renderAt(routes.adminErrors);
    fireEvent.click(screen.getByRole("button", { name: "Open menu" }));
    expect(screen.getAllByRole("link", { name: /Errors/, hidden: true })).toHaveLength(2);
  });
});

import { render, screen, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it } from "vitest";
import { CustomerLayout } from "@/app/layouts/customer-layout";
import { routes } from "@/constants/routes";
import { useAuthStore } from "@/stores/auth-store";

function renderLayout() {
  return render(
    <MemoryRouter initialEntries={[routes.home]}>
      <Routes>
        <Route element={<CustomerLayout />}>
          <Route path={routes.home} element={<p>home</p>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

function navLabels() {
  return screen
    .getAllByRole("navigation", { name: "Customer" })
    .map((nav) => within(nav).getAllByRole("link").map((link) => link.textContent));
}

describe("CustomerLayout navigation", () => {
  beforeEach(() => useAuthStore.getState().signOut());

  it("hides Bookings from signed-out visitors", () => {
    renderLayout();

    for (const labels of navLabels()) {
      expect(labels).toEqual(["Home", "Sign in"]);
    }
  });

  it("shows Bookings once the customer signs in", () => {
    useAuthStore
      .getState()
      .signIn("token", { id: "u1", name: "Test", phone: "+919000000000", gender: null, role: "Customer" });
    renderLayout();

    for (const labels of navLabels()) {
      expect(labels).toEqual(["Home", "Bookings", "Profile"]);
    }
  });

  it("shows the footer with packs and the three policy pages", () => {
    renderLayout();

    const footer = within(screen.getByRole("navigation", { name: "More" }));
    expect(footer.getAllByRole("link").map((link) => link.getAttribute("href"))).toEqual([
      routes.packs,
      routes.refundPolicy,
      routes.terms,
      routes.privacy,
    ]);
  });
});

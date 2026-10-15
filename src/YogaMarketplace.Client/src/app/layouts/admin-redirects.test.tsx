import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { LegacyAdminRedirect } from "@/app/layouts/admin-redirect";
import { legacyAdminPath } from "@/app/layouts/admin-redirects";
import { routes } from "@/constants/routes";
import { CitiesPage, FeesPage } from "@/features/admin/pages/settings-page";

describe("legacy admin paths", () => {
  it("keeps the rest of the query when a tab moves", () => {
    expect(legacyAdminPath("/admin/masters", "?tab=cities&page=2&state=abc")).toBe(
      `${routes.adminCities}?page=2&state=abc`,
    );
    expect(legacyAdminPath("/admin/masters", "")).toBe(routes.adminFinanceFees);
    expect(legacyAdminPath("/admin/transactions", "?tab=payouts&payout=Paid")).toBe(
      `${routes.adminPayouts}?payout=Paid`,
    );
    expect(legacyAdminPath("/admin/transactions", "?tab=reconciliation")).toBe(routes.adminReconciliation);
    expect(legacyAdminPath("/admin/transactions", "?status=Paid")).toBe(`${routes.adminTransactions}?status=Paid`);
    expect(legacyAdminPath("/admin/users/user-1", "?from=list")).toBe(`${routes.adminUsers}/user-1?from=list`);
    expect(legacyAdminPath("/admin/bookings/booking-1", "")).toBe(`${routes.adminBookings}/booking-1`);
    expect(legacyAdminPath("/admin/jobs", "")).toBe(routes.adminJobs);
    expect(legacyAdminPath("/admin/errors", "")).toBe(routes.adminErrors);
    expect(legacyAdminPath("/admin/approvals", "")).toBe(routes.adminApprovals);
  });

  it("redirects an old tab url onto the page", async () => {
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter initialEntries={["/admin/masters?tab=cities&page=2"]}>
          <Routes>
            <Route path="/admin/masters" element={<LegacyAdminRedirect />} />
            <Route path={routes.adminCities} element={<CitiesPage />} />
            <Route path={routes.adminFinanceFees} element={<FeesPage />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    expect(screen.getByRole("heading", { level: 1, name: "Cities" })).toBeInTheDocument();
  });

  it("renders the fees page on its route", async () => {
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter initialEntries={[routes.adminFinanceFees]}>
          <Routes>
            <Route path={routes.adminFinanceFees} element={<FeesPage />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );

    expect(screen.getByRole("heading", { level: 1, name: "Fees and payouts" })).toBeInTheDocument();
  });
});

import type { ComponentType } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { routes } from "@/constants/routes";
import { ApprovalsPage } from "@/features/admin/pages/approvals-page";
import { BookingsPage } from "@/features/admin/pages/bookings-page";
import { DashboardPage } from "@/features/admin/pages/dashboard-page";
import { ErrorsPage } from "@/features/admin/pages/errors-page";
import { JobsPage } from "@/features/admin/pages/jobs-page";
import {
  AlertsPage,
  AreasPage,
  BannerPage,
  CategoriesPage,
  CitiesPage,
  ConsentPage,
  CreditRulesPage,
  FeesPage,
  PacksPage,
  PolicyPagesPage,
  ReconciliationSettingsPage,
  SignInPage,
  StatesPage,
} from "@/features/admin/pages/settings-page";
import { PayoutsPage, ReconciliationPage, TransactionsPage } from "@/features/admin/pages/transactions-page";
import { UsersPage } from "@/features/admin/pages/users-page";

const { adminApi, adminCreditsApi, contentApi } = vi.hoisted(() => {
  const paged = { items: [], total: 0, page: 1, pageSize: 20 };
  const settings = {
    version: 1,
    commissionPercent: 15,
    convenienceFee: 0,
    cancelFreeWindowHours: 12,
    lateCancelFeeType: "Percent",
    payoutCycle: "Weekly",
    payoutPeriodStart: "2026-01-06",
    creditConvenienceFee: false,
    creditPayBasis: "PackPerSession",
    creditLateCancel: "ReturnCredit",
    creditBookingStandsAfterExpiry: true,
    packRefundsEnabled: false,
    packReferenceSessionPrice: 0,
    packRefundFee: 0,
    bannerTitle: "",
    bannerSubtitle: "",
    bannerOffer: "",
    policyVersion: 1,
    webhookPayloadRetentionDays: 30,
    timeZoneId: "UTC",
    nearestCityMaxKm: 100,
    reconciliationTime: "02:00",
    webhookStuckMinutes: 30,
    refundPendingHours: 24,
    otpMaxSendsPerPhone: 5,
    otpMaxWrongCodesPerPhone: 5,
    otpLockMinutes: 15,
    emailAlertsEnabled: false,
    errorAlertEmail: "",
    errorAlertThrottleMinutes: 60,
    errorAlertCheckMinutes: 5,
    errorEventRetentionDays: 30,
    emailNotificationsEnabled: false,
    notificationMaxAttempts: 3,
    reconciliationEnabled: true,
    creditExpiryEnabled: true,
    creditExpiryTime: "03:00",
    webhookCleanupEnabled: true,
    webhookCleanupTime: "04:00",
  };
  const page = {
    slug: "refund-policy",
    title: "Refunds",
    body: "Hello",
    version: 1,
    updatedAt: "2026-10-15T00:00:00Z",
  };
  function api(valueFor: (name: string) => unknown) {
    return new Proxy(
      {},
      {
        get: (_target, prop) => {
          if (typeof prop !== "string") return undefined;
          return vi.fn().mockResolvedValue(valueFor(prop));
        },
      },
    );
  }
  return {
    adminApi: api((name) => {
      if (name === "settings" || name === "updateSettings") return settings;
      if (name === "summary") return { bookingsByStatus: [], pendingPayouts: { net: 0, count: 0 } };
      if (name === "errorSummary") return { unacknowledged: 0 };
      if (name === "categories") return [{ id: "c", name: "Yoga", slug: "yoga", isActive: true }];
      if (["states", "cities", "jobs", "errors", "reconciliationRuns", "reconciliationItems"].includes(name))
        return paged;
      return [];
    }),
    adminCreditsApi: api(() => []),
    contentApi: api((name) => (name === "adminPages" ? [page] : page)),
  };
});

vi.mock("@/features/admin/api/admin-api", () => ({ adminApi }));
vi.mock("@/features/admin/api/admin-credits-api", () => ({ adminCreditsApi }));
vi.mock("@/features/content/api/content-api", () => ({ contentApi }));

const pages: { title: string; lead: string; Page: ComponentType }[] = [
  { title: "Home", lead: "What you earned, what you owe instructors, and work waiting today.", Page: DashboardPage },
  {
    title: "Approvals",
    lead: "Verify a pending instructor so customers can browse them. Approve a certificate to give them the Verified badge. Neither creates a booking.",
    Page: ApprovalsPage,
  },
  {
    title: "Bookings",
    lead: "Read-only list of marketplace bookings. Open a row for payment and payout detail.",
    Page: BookingsPage,
  },
  {
    title: "Users",
    lead: "Search by name or phone. Open a row for the full account. OTP codes are never shown here.",
    Page: UsersPage,
  },
  {
    title: "Errors",
    lead: "Server errors grouped by kind, newest first. Give the correlation id to your developer to find the matching log lines. Acknowledge an error once it is handled; it opens again if it happens again.",
    Page: ErrorsPage,
  },
  {
    title: "Jobs",
    lead: "Work the app does in the background: daily reconciliation, credit expiry and webhook clean-up. A job that keeps failing stops here with its error until you retry it.",
    Page: JobsPage,
  },
  { title: "Transactions", lead: "Customer payments and pack sales stay here.", Page: TransactionsPage },
  {
    title: "Payouts",
    lead: "When a session is completed, marked no-show, or cancelled late, a payout appears so you can send the instructor their share and keep your take.",
    Page: PayoutsPage,
  },
  { title: "Reconciliation", lead: "Where Razorpay and our records disagree each day.", Page: ReconciliationPage },
  {
    title: "Fees and payouts",
    lead: "Changes apply to bookings made after you save. Existing bookings keep the fees they were booked with.",
    Page: FeesPage,
  },
  {
    title: "Reconciliation settings",
    lead: "Once a day we compare Razorpay with our records for the previous day and list anything that differs under Reconciliation. Nothing is changed automatically.",
    Page: ReconciliationSettingsPage,
  },
  {
    title: "States",
    lead: "Add a state before its cities. A state stays on until every city in it is turned off.",
    Page: StatesPage,
  },
  {
    title: "Cities",
    lead: "Every city belongs to a state. Pick a state to see its cities, and to add another. Furthest city limits Use my location.",
    Page: CitiesPage,
  },
  {
    title: "Areas",
    lead: "Pick a state, then one of its cities, then add a neighbourhood. Hide a neighbourhood to take it out of browse. Existing bookings there carry on.",
    Page: AreasPage,
  },
  {
    title: "Categories",
    lead: "Customers see this name. The slug stays yoga so public browse does not break.",
    Page: CategoriesPage,
  },
  {
    title: "Session packs",
    lead: "Customers buy a pack once and book with credits. Changes apply to new purchases only. Packs can be turned off but never deleted, so past purchases keep their history.",
    Page: PacksPage,
  },
  {
    title: "Credit rules",
    lead: "Applies to new purchases only. Each purchase keeps the rules in force when it was bought.",
    Page: CreditRulesPage,
  },
  {
    title: "Policy pages",
    lead: "Refund policy, Terms and Privacy are linked in the site footer. Changes go live as soon as you save.",
    Page: PolicyPagesPage,
  },
  {
    title: "Home banner",
    lead: "The headline customers see on the home page. Leave a field empty to show the default text.",
    Page: BannerPage,
  },
  {
    title: "Consent and data",
    lead: "Raise the policy version after an important change to Terms or Privacy, and every user is asked once to accept again when they next sign in or their session renews.",
    Page: ConsentPage,
  },
  {
    title: "Sign-in limits",
    lead: "Stops one phone number from being flooded with codes or guessed at. Codes and wrong tries are counted over the lock time; reaching either limit locks that number for the lock time.",
    Page: SignInPage,
  },
  {
    title: "Alerts",
    lead: "Error emails, and emails to customers and instructors when a booking is requested, confirmed, cancelled or refunded.",
    Page: AlertsPage,
  },
];

describe("admin page titles", () => {
  it.each(pages)("$title appears once", async ({ title, lead, Page }) => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <MemoryRouter initialEntries={[routes.admin]}>
          <Page />
        </MemoryRouter>
      </QueryClientProvider>,
    );

    expect(await screen.findByRole("heading", { level: 1, name: title })).toBeInTheDocument();
    await waitFor(() => expect(client.isFetching()).toBe(0));
    expect(screen.getAllByRole("heading", { name: title })).toHaveLength(1);
    expect(screen.getAllByText(lead)).toHaveLength(1);
  });
});

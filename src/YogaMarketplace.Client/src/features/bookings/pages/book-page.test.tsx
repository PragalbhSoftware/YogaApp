import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { bookPath, routes } from "@/constants/routes";
import { BookPage } from "@/features/bookings/pages/book-page";
import type { BookingQuote, BookingRecord } from "@/features/bookings/types";
import { TermsPage } from "@/features/content/pages/content-page";
import { useAuthStore } from "@/stores/auth-store";
import { useBookingDraftStore } from "@/stores/booking-draft-store";

type CheckoutOptions = { onBooked: (booking: BookingRecord) => void };

const { checkoutOptions, page } = vi.hoisted(() => ({
  checkoutOptions: { current: null as CheckoutOptions | null },
  page: vi.fn(),
}));

const quote: BookingQuote = {
  slotId: "slot-1",
  serviceId: "service-1",
  sessionAmount: 900,
  convenienceFee: 50,
  total: 950,
  currency: "INR",
  isTrial: false,
  credit: {
    available: true,
    reason: null,
    message: null,
    convenienceFee: 0,
    total: 0,
    batchId: "batch-1",
    packName: "Starter pack",
    useBy: null,
  },
};

vi.mock("@/features/marketplace/hooks/use-instructors", () => ({
  useInstructor: () => ({
    data: {
      id: "provider-1",
      displayName: "Asha",
      areaOpen: true,
      modes: [{ mode: "Home", price: 900 }],
      trial: { price: 300 },
    },
    isLoading: false,
    isError: false,
    isSuccess: true,
  }),
  useInstructorOpenSlots: () => ({
    data: { slots: [{ id: "slot-1", date: "2099-01-05", start: "07:00", end: "08:00" }] },
    isLoading: false,
  }),
}));
vi.mock("@/features/bookings/hooks/use-bookings", () => ({
  useTrialStatus: () => ({ data: { offered: true, price: 300, used: false }, isLoading: false }),
  useBookingQuote: () => ({ data: quote, isLoading: false, isError: false, refetch: vi.fn() }),
}));
vi.mock("@/features/profile/hooks/use-profile", () => ({
  useCustomerProfile: () => ({ data: { visitAddress: null }, isLoading: false, isError: false, error: null }),
  useSaveVisitAddress: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));
vi.mock("@/features/bookings/hooks/use-book-checkout", () => ({
  useBookCheckout: (options: CheckoutOptions) => {
    checkoutOptions.current = options;
    return { checkout: vi.fn(), pending: false };
  },
}));
vi.mock("@/features/bookings/components/book-summary", () => ({ BookSummary: () => null }));
vi.mock("@/features/bookings/components/booking-steps", () => ({ BookingSteps: () => null }));
vi.mock("@/features/credits/components/credits-panel", () => ({ CreditsPanel: () => null }));
vi.mock("@/features/content/api/content-api", () => ({ contentApi: { page } }));

const bookUrl = bookPath("provider-1", "slot-1", "Home");

function renderCheckout() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[bookUrl]}>
        <Routes>
          <Route path={routes.book} element={<BookPage />} />
          <Route path={routes.terms} element={<TermsPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("BookPage policy links", () => {
  beforeEach(() => {
    useBookingDraftStore.getState().clearAll();
    checkoutOptions.current = null;
    page.mockResolvedValue({ slug: "terms", title: "Terms of use", body: "Be kind.", version: 1, updatedAt: "2026-10-01T06:30:00Z" });
  });

  afterEach(() => useAuthStore.setState({ token: null, user: null }));

  it("shows the pay agreement with same-tab links", () => {
    renderCheckout();

    expect(screen.getByText(/By paying you agree to the/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Terms" })).not.toHaveAttribute("target");
  });

  it("keeps the session type, payment choice and typed address after Terms and Back", async () => {
    renderCheckout();

    fireEvent.click(screen.getByRole("radio", { name: "Regular session" }));
    fireEvent.click(screen.getByRole("radio", { name: "Pay ₹950" }));
    fireEvent.change(screen.getByRole("textbox", { name: "House and street" }), { target: { value: "12 Lake Road" } });

    fireEvent.click(screen.getByRole("link", { name: "Terms" }));
    expect(await screen.findByRole("heading", { name: "Terms of use" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Back" }));

    expect(screen.getByRole("heading", { name: "Book this session" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "Regular session" })).toBeChecked();
    expect(screen.getByRole("radio", { name: "Pay ₹950" })).toBeChecked();
    expect(screen.getByRole("textbox", { name: "House and street" })).toHaveValue("12 Lake Road");
  });

  it("forgets the typed address once the session is booked", () => {
    renderCheckout();
    fireEvent.change(screen.getByRole("textbox", { name: "House and street" }), { target: { value: "12 Lake Road" } });
    expect(useBookingDraftStore.getState().drafts["slot-1"]?.address?.line1).toBe("12 Lake Road");

    act(() => checkoutOptions.current?.onBooked({ providerName: "Asha", mode: "Home" } as BookingRecord));

    expect(screen.getByText("You’re booked")).toBeInTheDocument();
    expect(useBookingDraftStore.getState().drafts).toEqual({});
  });

  it("forgets the typed address when the customer signs out", () => {
    useAuthStore
      .getState()
      .signIn("token", { id: "u1", name: "Test", phone: "+919000000000", gender: null, role: "Customer" });
    renderCheckout();
    fireEvent.change(screen.getByRole("textbox", { name: "House and street" }), { target: { value: "12 Lake Road" } });

    act(() => useAuthStore.getState().signOut());

    expect(useBookingDraftStore.getState().drafts).toEqual({});
  });
});

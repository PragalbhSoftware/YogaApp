import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useClearSessionOnSignOut } from "@/app/providers/use-clear-session-on-sign-out";
import { profileBadgesQueryKey, useProfileBadges } from "@/features/profile/hooks/use-profile";
import { invalidateBookingLists } from "@/features/bookings/hooks/use-bookings";
import { useAuthStore } from "@/stores/auth-store";

const badgesMock = vi.hoisted(() => vi.fn());

vi.mock("@/features/profile/api/profile-api", () => ({
  profileApi: {
    badges: () => badgesMock(),
    me: vi.fn(),
    updateAccount: vi.fn(),
    savePreferredCity: vi.fn(),
    saveVisitAddress: vi.fn(),
  },
}));

function wrapper(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe("useProfileBadges", () => {
  beforeEach(() => {
    badgesMock.mockReset();
    badgesMock.mockResolvedValue({ bookings: 2, requests: 0 });
    useAuthStore.getState().signOut();
  });

  it("does not call badges for guests", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useProfileBadges(), { wrapper: wrapper(client) });
    await Promise.resolve();
    expect(badgesMock).not.toHaveBeenCalled();
  });

  it("does not call badges for admin", async () => {
    useAuthStore
      .getState()
      .signIn("token", { id: "a1", name: "Admin", phone: "+919000000001", gender: null, role: "Admin" });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useProfileBadges(), { wrapper: wrapper(client) });
    await Promise.resolve();
    expect(badgesMock).not.toHaveBeenCalled();
  });

  it("loads badges for a customer and refreshes after booking list invalidation", async () => {
    useAuthStore
      .getState()
      .signIn("token", { id: "c1", name: "Cust", phone: "+919000000002", gender: null, role: "Customer" });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useProfileBadges(), { wrapper: wrapper(client) });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual({ bookings: 2, requests: 0 });
    expect(badgesMock).toHaveBeenCalledTimes(1);

    badgesMock.mockResolvedValueOnce({ bookings: 5, requests: 0 });
    invalidateBookingLists(client);
    await waitFor(() => expect(result.current.data?.bookings).toBe(5));
    expect(badgesMock).toHaveBeenCalledTimes(2);
  });

  it("does not show user A's badge count after B signs in", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useClearSessionOnSignOut(client), { wrapper: wrapper(client) });

    useAuthStore
      .getState()
      .signIn("token-a", { id: "a", name: "A", phone: "+919000000003", gender: null, role: "Customer" });
    badgesMock.mockResolvedValue({ bookings: 7, requests: 0 });
    const first = renderHook(() => useProfileBadges(), { wrapper: wrapper(client) });
    await waitFor(() => expect(first.result.current.data?.bookings).toBe(7));
    first.unmount();

    act(() => {
      useAuthStore.getState().signOut();
    });
    await waitFor(() => expect(client.getQueryData(profileBadgesQueryKey)).toBeUndefined());

    badgesMock.mockResolvedValue({ bookings: 0, requests: 0 });
    useAuthStore
      .getState()
      .signIn("token-b", { id: "b", name: "B", phone: "+919000000004", gender: null, role: "Customer" });
    const second = renderHook(() => useProfileBadges(), { wrapper: wrapper(client) });
    await waitFor(() => expect(second.result.current.isSuccess).toBe(true));
    expect(second.result.current.data).toEqual({ bookings: 0, requests: 0 });
    expect(client.getQueryData(profileBadgesQueryKey)).toEqual({ bookings: 0, requests: 0 });
  });
});

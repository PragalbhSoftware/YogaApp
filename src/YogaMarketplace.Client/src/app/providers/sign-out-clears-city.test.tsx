import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useClearSessionOnSignOut } from "@/app/providers/use-clear-session-on-sign-out";
import { usePreferredCitySync } from "@/features/marketplace/hooks/use-preferred-city-sync";
import { useAreaStore } from "@/stores/area-store";
import { useAuthStore } from "@/stores/auth-store";
import { useCityStore } from "@/stores/city-store";

const savePreferredCity = vi.hoisted(() => vi.fn());

vi.mock("@/features/profile/api/profile-api", () => ({
  profileApi: {
    savePreferredCity: (...args: unknown[]) => savePreferredCity(...args),
    me: vi.fn(),
    badges: vi.fn(),
    updateAccount: vi.fn(),
    saveVisitAddress: vi.fn(),
  },
}));

vi.mock("@/features/marketplace/hooks/use-cities", () => ({
  useCities: () => ({ isSuccess: true, data: [{ id: "city-a" }] }),
}));

vi.mock("@/features/profile/hooks/use-profile", () => ({
  profileQueryKey: ["profile"],
  profileBadgesQueryKey: ["profile-badges"],
  useCustomerProfile: () => {
    const user = useAuthStore((state) => state.user);
    if (!user) return { isSuccess: false, data: undefined };
    return {
      isSuccess: true,
      data: { preferredCityId: user.id === "a" ? "city-a" : null },
    };
  },
}));

function wrapper(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

function useSignOutAndCitySync(client: QueryClient) {
  useClearSessionOnSignOut(client);
  usePreferredCitySync();
}

describe("sign-out clears persisted city for the next user", () => {
  beforeEach(async () => {
    savePreferredCity.mockReset();
    savePreferredCity.mockResolvedValue({ preferredCityId: null });
    useAuthStore.getState().signOut();
    useCityStore.getState().clearCity();
    useAreaStore.getState().clearArea();
    await useCityStore.persist.rehydrate();
  });

  it("drops user A's city so user B with no preferred city does not inherit it or save it", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useSignOutAndCitySync(client), { wrapper: wrapper(client) });

    act(() => {
      useCityStore.getState().setCityId("city-a");
      useAreaStore.getState().setArea("Area A");
      useAuthStore
        .getState()
        .signIn("token-a", { id: "a", name: "A", phone: "+919000000010", gender: null, role: "Customer" });
    });
    expect(useCityStore.getState().cityId).toBe("city-a");

    act(() => {
      useAuthStore.getState().signOut();
    });
    await waitFor(() => expect(useCityStore.getState().cityId).toBeNull());
    expect(useAreaStore.getState().areaName).toBeNull();

    act(() => {
      useAuthStore
        .getState()
        .signIn("token-b", { id: "b", name: "B", phone: "+919000000011", gender: null, role: "Customer" });
    });

    await act(async () => {
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(useCityStore.getState().cityId).toBeNull();
    expect(useAreaStore.getState().areaName).toBeNull();
    expect(savePreferredCity).not.toHaveBeenCalled();
  });
});

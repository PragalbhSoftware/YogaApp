import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useEffect } from "react";
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

/** Mirrors app-providers clearing city/area when the session ends. */
function useClearCityOnSignOut() {
  useEffect(
    () =>
      useAuthStore.subscribe((state, previous) => {
        if (previous.user && !state.user) {
          useCityStore.getState().clearCity();
          useAreaStore.getState().clearArea();
        }
      }),
    [],
  );
}

describe("sign-out clears persisted city for the next user", () => {
  beforeEach(() => {
    savePreferredCity.mockReset();
    useAuthStore.getState().signOut();
    useCityStore.getState().clearCity();
    useAreaStore.getState().clearArea();
  });

  it("drops user A's city so user B with no preferred city does not inherit it or save it", async () => {
    renderHook(() => useClearCityOnSignOut());

    act(() => {
      useAuthStore
        .getState()
        .signIn("token-a", { id: "a", name: "A", phone: "+919000000010", gender: null, role: "Customer" });
      useCityStore.getState().setCityId("city-a");
      useAreaStore.getState().setArea("Area A");
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
    expect(useCityStore.getState().cityId).toBeNull();
    expect(savePreferredCity).not.toHaveBeenCalled();
  });
});

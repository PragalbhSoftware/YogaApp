import { QueryClient } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { syncPreferredCity } from "@/features/marketplace/utils/sync-preferred-city";
import { profileApi } from "@/features/profile/api/profile-api";
import { useAreaStore } from "@/stores/area-store";
import { useAuthStore } from "@/stores/auth-store";
import { useCityStore } from "@/stores/city-store";

vi.mock("@/features/profile/api/profile-api", () => ({
  profileApi: { savePreferredCity: vi.fn() },
}));

const cities = [
  { id: "c1" },
  { id: "c2" },
];

describe("syncPreferredCity", () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient();
    vi.mocked(profileApi.savePreferredCity).mockReset();
    vi.mocked(profileApi.savePreferredCity).mockResolvedValue({} as never);
    useCityStore.getState().clearCity();
    useCityStore.getState().clearPreferredCitySyncPending();
    useAreaStore.getState().clearArea();
    useAuthStore.getState().signOut();
    useAuthStore.getState().signIn("token", {
      id: "u1",
      name: "Test",
      phone: "+919700000001",
      gender: "Female",
      role: "Customer",
    });
  });

  it("uses the profile city when it differs from the browser and clears the neighbourhood", async () => {
    useCityStore.getState().setCityId("c2");
    useAreaStore.getState().setArea("Old area");

    await syncPreferredCity("c1", cities, "c2", "u1", queryClient);

    expect(useCityStore.getState().cityId).toBe("c1");
    expect(useAreaStore.getState().areaName).toBeNull();
    expect(profileApi.savePreferredCity).not.toHaveBeenCalled();
  });

  it("saves the browser city when the profile has none", async () => {
    useCityStore.getState().setCityId("c2");

    await syncPreferredCity(null, cities, "c2", "u1", queryClient);

    expect(profileApi.savePreferredCity).toHaveBeenCalledWith("c2");
    expect(useCityStore.getState().cityId).toBe("c2");
  });

  it("clears an inactive profile city and the neighbourhood", async () => {
    useCityStore.getState().setCityId("gone");
    useAreaStore.getState().setArea("Old area");
    vi.mocked(profileApi.savePreferredCity).mockResolvedValue({ preferredCityId: null } as never);

    await syncPreferredCity("gone", cities, "gone", "u1", queryClient);

    expect(useCityStore.getState().cityId).toBeNull();
    expect(useAreaStore.getState().areaName).toBeNull();
    expect(profileApi.savePreferredCity).toHaveBeenCalledWith(null);
  });

  it("retries a pending push instead of letting the profile overwrite the browser", async () => {
    useCityStore.getState().setCityId("c2");
    useCityStore.getState().markPreferredCitySyncPending("u1");

    await syncPreferredCity("c1", cities, "c2", "u1", queryClient);

    expect(profileApi.savePreferredCity).toHaveBeenCalledWith("c2");
    expect(useCityStore.getState().cityId).toBe("c2");
    expect(useCityStore.getState().preferredCitySyncPending).toBeNull();
  });

  it("after user A's save fails and A logs out, user B keeps their profile city", async () => {
    useCityStore.getState().setCityId("c2");
    useCityStore.getState().markPreferredCitySyncPending("user-a");
    useAuthStore.getState().signOut();
    expect(useCityStore.getState().preferredCitySyncPending).toBeNull();
    expect(useCityStore.getState().cityId).toBe("c2");

    useAuthStore.getState().signIn("token-b", {
      id: "user-b",
      name: "Bee",
      phone: "+919700000002",
      gender: "Other",
      role: "Customer",
    });

    await syncPreferredCity("c1", cities, "c2", "user-b", queryClient);

    expect(profileApi.savePreferredCity).not.toHaveBeenCalled();
    expect(useCityStore.getState().cityId).toBe("c1");
    expect(useCityStore.getState().preferredCitySyncPending).toBeNull();
  });

  it("drops another user's leftover pending instead of pushing it", async () => {
    useCityStore.getState().setCityId("c2");
    useCityStore.getState().markPreferredCitySyncPending("user-a");

    await syncPreferredCity("c1", cities, "c2", "user-b", queryClient);

    expect(profileApi.savePreferredCity).not.toHaveBeenCalled();
    expect(useCityStore.getState().cityId).toBe("c1");
    expect(useCityStore.getState().preferredCitySyncPending).toBeNull();
  });
});

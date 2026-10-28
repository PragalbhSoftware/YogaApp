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
  beforeEach(() => {
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

    await syncPreferredCity("c1", cities, "c2");

    expect(useCityStore.getState().cityId).toBe("c1");
    expect(useAreaStore.getState().areaName).toBeNull();
    expect(profileApi.savePreferredCity).not.toHaveBeenCalled();
  });

  it("saves the browser city when the profile has none", async () => {
    useCityStore.getState().setCityId("c2");

    await syncPreferredCity(null, cities, "c2");

    expect(profileApi.savePreferredCity).toHaveBeenCalledWith("c2");
    expect(useCityStore.getState().cityId).toBe("c2");
  });

  it("clears an inactive profile city and the neighbourhood", async () => {
    useCityStore.getState().setCityId("gone");
    useAreaStore.getState().setArea("Old area");
    vi.mocked(profileApi.savePreferredCity).mockResolvedValue({ preferredCityId: null } as never);

    await syncPreferredCity("gone", cities, "gone");

    expect(useCityStore.getState().cityId).toBeNull();
    expect(useAreaStore.getState().areaName).toBeNull();
    expect(profileApi.savePreferredCity).toHaveBeenCalledWith(null);
  });

  it("retries a pending push instead of letting the profile overwrite the browser", async () => {
    useCityStore.getState().setCityId("c2");
    useCityStore.getState().markPreferredCitySyncPending();

    await syncPreferredCity("c1", cities, "c2");

    expect(profileApi.savePreferredCity).toHaveBeenCalledWith("c2");
    expect(useCityStore.getState().cityId).toBe("c2");
    expect(useCityStore.getState().preferredCitySyncPending).toBe(false);
  });
});

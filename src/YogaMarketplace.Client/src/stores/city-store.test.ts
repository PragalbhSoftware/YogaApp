import { beforeEach, describe, expect, it } from "vitest";
import { useCityStore } from "@/stores/city-store";

describe("city store", () => {
  beforeEach(() => {
    useCityStore.getState().clearCity();
    useCityStore.getState().clearPreferredCitySyncPending();
    localStorage.clear();
  });

  it("saves the selected city id", () => {
    useCityStore.getState().setCityId("city-1");
    expect(useCityStore.getState().cityId).toBe("city-1");
    expect(JSON.parse(localStorage.getItem("ym.city") ?? "{}").state.cityId).toBe("city-1");
  });

  it("keeps a city saved under the previous persist shape", async () => {
    localStorage.setItem("ym.city", JSON.stringify({ state: { cityId: "city-1" }, version: 1 }));
    await useCityStore.persist.rehydrate();
    expect(useCityStore.getState().cityId).toBe("city-1");
    expect(useCityStore.getState().preferredCitySyncPending).toBeNull();
  });
});

import { beforeEach, describe, expect, it } from "vitest";
import { useCityStore } from "@/stores/city-store";

describe("city store", () => {
  beforeEach(() => {
    useCityStore.getState().clearCity();
    localStorage.clear();
  });

  it("saves the selected city id", () => {
    useCityStore.getState().setCityId("city-1");
    expect(useCityStore.getState().cityId).toBe("city-1");
    expect(JSON.parse(localStorage.getItem("ym.city") ?? "{}").state.cityId).toBe("city-1");
  });
});

import { describe, expect, it } from "vitest";
import { migrateArea } from "@/stores/area-store";

describe("saved area migration", () => {
  it("clears a legacy pick that has no city instead of guessing one", () => {
    expect(migrateArea({ areaName: "Bandra" }, 0)).toEqual({ areaName: null });
  });

  it("drops a saved city name that has no city id so the picker shows once", () => {
    expect(migrateArea({ city: "Harbor", areaName: "Kothrud" }, 1)).toEqual({ areaName: null });
  });

  it("keeps a neighbourhood saved without a city name", () => {
    expect(migrateArea({ areaName: "Kothrud" }, 2)).toEqual({ areaName: "Kothrud" });
  });

  it("clears a half-saved pick", () => {
    expect(migrateArea({ cityId: "c1", areaName: null }, 2)).toEqual({ areaName: null });
    expect(migrateArea(undefined, 2)).toEqual({ areaName: null });
  });
});

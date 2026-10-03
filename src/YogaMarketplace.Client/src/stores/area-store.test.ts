import { describe, expect, it } from "vitest";
import { migrateArea } from "@/stores/area-store";

describe("saved area migration", () => {
  it("clears a legacy pick that has no city instead of guessing one", () => {
    expect(migrateArea({ areaName: "Bandra" }, 0)).toEqual({ city: null, areaName: null });
  });

  it("keeps a complete pick", () => {
    expect(migrateArea({ city: "Pune", areaName: "Kothrud" }, 1)).toEqual({ city: "Pune", areaName: "Kothrud" });
  });

  it("clears a half-saved pick", () => {
    expect(migrateArea({ city: "Pune", areaName: null }, 1)).toEqual({ city: null, areaName: null });
    expect(migrateArea(undefined, 1)).toEqual({ city: null, areaName: null });
  });
});

import { afterEach, describe, expect, it } from "vitest";
import { defaultMarketTimeZone, formatWhenKolkata, marketTimeZone, setMarketTimeZone } from "@/utils/clock";

describe("market clock", () => {
  afterEach(() => setMarketTimeZone(defaultMarketTimeZone));

  it("formats an instant in the zone from config", () => {
    setMarketTimeZone("UTC");
    expect(marketTimeZone()).toBe("UTC");
    expect(formatWhenKolkata("2026-01-15T00:30:00Z")).toMatch(/12:30/);

    setMarketTimeZone(defaultMarketTimeZone);
    expect(formatWhenKolkata("2026-01-15T00:30:00Z")).toMatch(/6:00/);
  });
});

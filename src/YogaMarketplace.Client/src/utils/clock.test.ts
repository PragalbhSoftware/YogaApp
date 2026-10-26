import { afterEach, describe, expect, it, vi } from "vitest";
import {
  defaultMarketTimeZone,
  formatWhenKolkata,
  loadMarketTimeZone,
  marketTimeZone,
  setMarketTimeZone,
} from "@/utils/clock";

describe("market clock", () => {
  afterEach(() => {
    setMarketTimeZone(defaultMarketTimeZone);
    vi.unstubAllGlobals();
  });

  it("formats an instant in the zone from config", () => {
    setMarketTimeZone("UTC");
    expect(marketTimeZone()).toBe("UTC");
    expect(formatWhenKolkata("2026-01-15T00:30:00Z")).toMatch(/12:30/);

    setMarketTimeZone(defaultMarketTimeZone);
    expect(formatWhenKolkata("2026-01-15T00:30:00Z")).toMatch(/6:00/);
  });

  it("keeps the default zone when the id is not one Intl can use", () => {
    setMarketTimeZone("Europe/London");
    expect(marketTimeZone()).toBe("Europe/London");

    setMarketTimeZone("Not/AZone");
    expect(marketTimeZone()).toBe(defaultMarketTimeZone);
  });

  it("loads an IANA id from config and keeps the default when the fetch fails", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({ timeZoneId: "Europe/London" }),
      }),
    );
    await loadMarketTimeZone(AbortSignal.timeout(3000));
    expect(marketTimeZone()).toBe("Europe/London");
  });

  it("keeps the default zone when the config fetch is aborted", async () => {
    setMarketTimeZone(defaultMarketTimeZone);
    vi.stubGlobal(
      "fetch",
      vi.fn().mockRejectedValue(new DOMException("The operation was aborted", "AbortError")),
    );
    await loadMarketTimeZone(AbortSignal.abort());
    expect(marketTimeZone()).toBe(defaultMarketTimeZone);
  });

  it("keeps the default when config returns an id Intl rejects", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({ timeZoneId: "Not/AZone" }),
      }),
    );
    await loadMarketTimeZone();
    expect(marketTimeZone()).toBe(defaultMarketTimeZone);
  });
});

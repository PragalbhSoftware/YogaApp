import { describe, expect, it } from "vitest";
import indexHtml from "../../index.html?raw";
import { site } from "@/constants/site";
import { pageTitle } from "@/hooks/use-page-title";

describe("site copy", () => {
  it("uses the exact tagline in the index.html meta description", () => {
    const doc = new DOMParser().parseFromString(indexHtml, "text/html");
    const meta = doc.querySelector('meta[name="description"]');
    expect(meta?.getAttribute("content")).toBe(site.tagline);
    expect(doc.title).toBe(site.name);
  });

  it("never names a city in the product name or tagline", () => {
    expect(`${site.name} ${site.tagline}`).not.toMatch(/mumbai|pune|delhi|bengaluru|india/i);
  });

  it("formats page titles with the product name", () => {
    expect(pageTitle("My bookings")).toBe("My bookings · Yoga Marketplace");
    expect(pageTitle(null)).toBe("Yoga Marketplace");
    expect(pageTitle("  ")).toBe("Yoga Marketplace");
  });
});

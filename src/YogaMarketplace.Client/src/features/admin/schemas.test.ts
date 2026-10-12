import { describe, expect, it } from "vitest";
import { bannerSchema, creditSettingsSchema, feeSettingsSchema, packSchema } from "@/features/admin/schemas";

const valid = {
  commissionPercent: 15,
  convenienceFee: 0,
  cancelFreeWindowHours: 12,
  lateCancelFeeType: "Percent",
  lateCancelFeeValue: 50,
  payoutCycle: "Weekly",
  policyNote: "",
};

function errorPaths(input: Record<string, unknown>) {
  const result = feeSettingsSchema.safeParse({ ...valid, ...input });
  return result.success ? [] : result.error.issues.map((issue) => issue.path.join("."));
}

describe("fee settings form", () => {
  it("accepts the edges: 0 h window and 0% or 100% fees", () => {
    expect(errorPaths({ cancelFreeWindowHours: 0, lateCancelFeeValue: 0, commissionPercent: 0 })).toEqual([]);
    expect(errorPaths({ lateCancelFeeValue: 100, commissionPercent: 100 })).toEqual([]);
  });

  it("reads typed text as numbers", () => {
    expect(errorPaths({ commissionPercent: "12.5", cancelFreeWindowHours: "24" })).toEqual([]);
  });

  it("rejects out-of-range values and more than 2 decimals", () => {
    expect(errorPaths({ commissionPercent: 101 })).toEqual(["commissionPercent"]);
    expect(errorPaths({ commissionPercent: 15.555 })).toEqual(["commissionPercent"]);
    expect(errorPaths({ convenienceFee: -1 })).toEqual(["convenienceFee"]);
    expect(errorPaths({ cancelFreeWindowHours: 169 })).toEqual(["cancelFreeWindowHours"]);
    expect(errorPaths({ cancelFreeWindowHours: 1.5 })).toEqual(["cancelFreeWindowHours"]);
  });

  it("checks the late fee against its type", () => {
    expect(errorPaths({ lateCancelFeeValue: 250 })).toEqual(["lateCancelFeeValue"]);
    expect(errorPaths({ lateCancelFeeType: "Flat", lateCancelFeeValue: 250 })).toEqual([]);
    expect(errorPaths({ lateCancelFeeType: "Flat", lateCancelFeeValue: 10_001 })).toEqual(["lateCancelFeeValue"]);
  });
});

describe("pack form", () => {
  const pack = { name: "8 sessions", sessions: 8, price: 4800, validityDays: 30, priceCap: 800 };
  const packErrors = (input: Record<string, unknown>) => {
    const result = packSchema.safeParse({ ...pack, ...input });
    return result.success ? [] : result.error.issues.map((issue) => issue.path.join("."));
  };

  it("accepts a normal pack typed as text", () => {
    expect(packErrors({ sessions: "8", price: "4800.50", validityDays: "30", priceCap: "800" })).toEqual([]);
  });

  it("rejects zero or fractional sessions, prices under ₹1 and long validity", () => {
    expect(packErrors({ sessions: 0 })).toEqual(["sessions"]);
    expect(packErrors({ sessions: 2.5 })).toEqual(["sessions"]);
    expect(packErrors({ price: 0.5 })).toEqual(["price"]);
    expect(packErrors({ price: 99.999 })).toEqual(["price"]);
    expect(packErrors({ validityDays: 731 })).toEqual(["validityDays"]);
    expect(packErrors({ priceCap: 0 })).toEqual(["priceCap"]);
  });
});

describe("credit settings form", () => {
  const credit = {
    creditConvenienceFee: false,
    creditPayBasis: "PackPerSession",
    creditLateCancel: "PayInstructorShare",
    creditBookingStandsAfterExpiry: true,
    packRefundsEnabled: true,
    packReferenceSessionPrice: 799,
    packRefundFee: 0,
  };

  it("accepts the defaults and rejects unknown rules or negative fees", () => {
    expect(creditSettingsSchema.safeParse(credit).success).toBe(true);
    expect(creditSettingsSchema.safeParse({ ...credit, creditLateCancel: "Refund" }).success).toBe(false);
    expect(creditSettingsSchema.safeParse({ ...credit, packRefundFee: -1 }).success).toBe(false);
  });
});

describe("banner form", () => {
  it("allows empty fields so the home page falls back to default copy", () => {
    expect(bannerSchema.safeParse({ bannerTitle: "", bannerSubtitle: "", bannerOffer: "" }).success).toBe(true);
  });

  it("limits the title length", () => {
    const result = bannerSchema.safeParse({ bannerTitle: "a".repeat(81), bannerSubtitle: "", bannerOffer: "" });
    expect(result.success).toBe(false);
  });
});

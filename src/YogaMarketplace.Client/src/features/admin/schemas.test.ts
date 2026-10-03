import { describe, expect, it } from "vitest";
import { bannerSchema, feeSettingsSchema } from "@/features/admin/schemas";

const valid = {
  commissionPercent: 15,
  convenienceFee: 0,
  cancelFreeWindowHours: 12,
  rescheduleFreeWindowHours: 12,
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
    expect(errorPaths({ rescheduleFreeWindowHours: 1.5 })).toEqual(["rescheduleFreeWindowHours"]);
  });

  it("checks the late fee against its type", () => {
    expect(errorPaths({ lateCancelFeeValue: 250 })).toEqual(["lateCancelFeeValue"]);
    expect(errorPaths({ lateCancelFeeType: "Flat", lateCancelFeeValue: 250 })).toEqual([]);
    expect(errorPaths({ lateCancelFeeType: "Flat", lateCancelFeeValue: 10_001 })).toEqual(["lateCancelFeeValue"]);
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

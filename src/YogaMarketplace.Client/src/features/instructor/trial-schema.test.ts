import { describe, expect, it } from "vitest";
import { toTrialPayload, trialFormFromSettings, trialSchema } from "@/features/instructor/trial-schema";

describe("trialSchema", () => {
  it("needs a rupee price only when the trial is on", () => {
    expect(trialSchema.safeParse({ enabled: false, price: "" }).success).toBe(true);
    expect(trialSchema.safeParse({ enabled: true, price: "0" }).success).toBe(true);
    expect(trialSchema.safeParse({ enabled: true, price: "199.50" }).success).toBe(true);
    expect(trialSchema.safeParse({ enabled: true, price: "" }).success).toBe(false);
    expect(trialSchema.safeParse({ enabled: true, price: "-5" }).success).toBe(false);
    expect(trialSchema.safeParse({ enabled: true, price: "1.999" }).success).toBe(false);
  });

  it("maps to and from the API shape", () => {
    expect(toTrialPayload({ enabled: true, price: "0" })).toEqual({ enabled: true, price: 0 });
    expect(toTrialPayload({ enabled: false, price: "99" })).toEqual({ enabled: false });
    expect(
      trialFormFromSettings({ enabled: false, price: 99, lowestRegularRate: 599, convenienceFee: 25 }),
    ).toEqual({ enabled: false, price: "99" });
  });
});

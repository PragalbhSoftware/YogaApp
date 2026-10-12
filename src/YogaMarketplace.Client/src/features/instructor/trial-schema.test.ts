import { describe, expect, it } from "vitest";
import { toTrialPayload, trialFormFromSettings, trialSchema } from "@/features/instructor/trial-schema";
import type { ProviderService } from "@/features/instructor/types";

const service = (id: string, isActive = true): ProviderService => ({
  id,
  title: `Service ${id}`,
  category: "Hatha",
  isActive,
  trialEnabled: false,
  trialPrice: null,
});

describe("trialSchema", () => {
  it("needs a rupee price and a service only when the trial is on", () => {
    expect(trialSchema.safeParse({ serviceId: "", enabled: false, price: "" }).success).toBe(true);
    expect(trialSchema.safeParse({ serviceId: "a", enabled: true, price: "0" }).success).toBe(true);
    expect(trialSchema.safeParse({ serviceId: "a", enabled: true, price: "199.50" }).success).toBe(true);
    expect(trialSchema.safeParse({ serviceId: "a", enabled: true, price: "" }).success).toBe(false);
    expect(trialSchema.safeParse({ serviceId: "a", enabled: true, price: "-5" }).success).toBe(false);
    expect(trialSchema.safeParse({ serviceId: "a", enabled: true, price: "1.999" }).success).toBe(false);
    expect(trialSchema.safeParse({ serviceId: "", enabled: true, price: "99" }).success).toBe(false);
  });

  it("maps to and from the API shape", () => {
    expect(toTrialPayload({ serviceId: "a", enabled: true, price: "0" })).toEqual({
      serviceId: "a",
      enabled: true,
      price: 0,
    });
    expect(toTrialPayload({ serviceId: "a", enabled: false, price: "99" })).toEqual({ enabled: false });
  });

  it("starts on the current trial service, else the first active one", () => {
    const trial = { serviceId: "b", enabled: false, price: 99, lowestRegularRate: 599, convenienceFee: 25 };
    expect(trialFormFromSettings(trial, [service("a"), service("b")])).toEqual({
      serviceId: "b",
      enabled: false,
      price: "99",
    });
    expect(trialFormFromSettings({ ...trial, serviceId: null }, [service("x", false), service("a")]).serviceId).toBe(
      "a",
    );
    expect(trialFormFromSettings(trial, []).serviceId).toBe("");
  });
});

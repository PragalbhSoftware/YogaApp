import { describe, expect, it } from "vitest";
import {
  bannerSchema,
  creditSettingsSchema,
  errorAlertSettingsSchema,
  notificationSettingsSchema,
  feeSettingsSchema,
  jobScheduleSettingsSchema,
  packSchema,
  privacySettingsSchema,
  reconciliationSettingsSchema,
  resolveItemSchema,
  signInLimitSettingsSchema,
} from "@/features/admin/schemas";

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

describe("reconciliation settings form", () => {
  const recon = { timeZoneId: "UTC", reconciliationTime: "06:00", webhookStuckMinutes: 60, refundPendingHours: 48 };
  const reconErrors = (input: Record<string, unknown>) => {
    const result = reconciliationSettingsSchema.safeParse({ ...recon, ...input });
    return result.success ? [] : result.error.issues.map((issue) => issue.path.join("."));
  };

  it("accepts the defaults and typed numbers", () => {
    expect(reconErrors({ webhookStuckMinutes: "15", refundPendingHours: "720" })).toEqual([]);
  });

  it("rejects bad times and thresholds out of range", () => {
    expect(reconErrors({ reconciliationTime: "24:00" })).toEqual(["reconciliationTime"]);
    expect(reconErrors({ reconciliationTime: "6:00" })).toEqual(["reconciliationTime"]);
    expect(reconErrors({ webhookStuckMinutes: 0 })).toEqual(["webhookStuckMinutes"]);
    expect(reconErrors({ refundPendingHours: 721 })).toEqual(["refundPendingHours"]);
    expect(reconErrors({ refundPendingHours: 1.5 })).toEqual(["refundPendingHours"]);
  });
});

describe("job schedules form", () => {
  const schedules = {
    reconciliationEnabled: true,
    creditExpiryEnabled: false,
    creditExpiryTime: "00:00",
    webhookCleanupEnabled: true,
    webhookCleanupTime: "02:00",
  };
  const scheduleErrors = (input: Record<string, unknown>) => {
    const result = jobScheduleSettingsSchema.safeParse({ ...schedules, ...input });
    return result.success ? [] : result.error.issues.map((issue) => issue.path.join("."));
  };

  it("accepts on/off switches and HH:mm times", () => {
    expect(scheduleErrors({})).toEqual([]);
    expect(scheduleErrors({ creditExpiryTime: "23:59" })).toEqual([]);
  });

  it("rejects times that are not HH:mm", () => {
    expect(scheduleErrors({ creditExpiryTime: "2:00" })).toEqual(["creditExpiryTime"]);
    expect(scheduleErrors({ webhookCleanupTime: "24:00" })).toEqual(["webhookCleanupTime"]);
  });
});

describe("error alerts form", () => {
  const alerts = {
    emailAlertsEnabled: false,
    errorAlertEmail: "",
    errorAlertThrottleMinutes: 30,
    errorAlertCheckMinutes: 5,
    errorEventRetentionDays: 30,
  };
  const alertErrors = (input: Record<string, unknown>) => {
    const result = errorAlertSettingsSchema.safeParse({ ...alerts, ...input });
    return result.success ? [] : result.error.issues.map((issue) => issue.path.join("."));
  };

  it("keeps alerts off without an email and turns them on with one", () => {
    expect(alertErrors({})).toEqual([]);
    expect(alertErrors({ emailAlertsEnabled: true, errorAlertEmail: " ops@example.com " })).toEqual([]);
  });

  it("needs a valid email before alerts can be turned on", () => {
    expect(alertErrors({ emailAlertsEnabled: true })).toEqual(["errorAlertEmail"]);
    expect(alertErrors({ errorAlertEmail: "not an email" })).toEqual(["errorAlertEmail"]);
  });

  it("rejects limits out of range", () => {
    expect(alertErrors({ errorAlertThrottleMinutes: 0 })).toEqual(["errorAlertThrottleMinutes"]);
    expect(alertErrors({ errorAlertCheckMinutes: 1441 })).toEqual(["errorAlertCheckMinutes"]);
    expect(alertErrors({ errorEventRetentionDays: 2.5 })).toEqual(["errorEventRetentionDays"]);
  });
});

describe("booking emails form", () => {
  const notificationErrors = (input: Record<string, unknown>) => {
    const result = notificationSettingsSchema.safeParse({ emailNotificationsEnabled: false, notificationMaxAttempts: 5, ...input });
    return result.success ? [] : result.error.issues.map((issue) => issue.path.join("."));
  };

  it("accepts the flag and a whole number of tries from 1 to 10", () => {
    expect(notificationErrors({ emailNotificationsEnabled: true, notificationMaxAttempts: "10" })).toEqual([]);
    expect(notificationErrors({ notificationMaxAttempts: 1 })).toEqual([]);
  });

  it("rejects tries out of range or not whole", () => {
    expect(notificationErrors({ notificationMaxAttempts: 0 })).toEqual(["notificationMaxAttempts"]);
    expect(notificationErrors({ notificationMaxAttempts: 11 })).toEqual(["notificationMaxAttempts"]);
    expect(notificationErrors({ notificationMaxAttempts: 2.5 })).toEqual(["notificationMaxAttempts"]);
  });
});

describe("sign-in limits form", () => {
  const limitErrors = (input: Record<string, unknown>) => {
    const result = signInLimitSettingsSchema.safeParse({
      otpMaxSendsPerPhone: 5,
      otpMaxWrongCodesPerPhone: 10,
      otpLockMinutes: 30,
      ...input,
    });
    return result.success ? [] : result.error.issues.map((issue) => issue.path.join("."));
  };

  it("accepts the range ends", () => {
    expect(limitErrors({ otpMaxSendsPerPhone: 1, otpMaxWrongCodesPerPhone: 1, otpLockMinutes: 1 })).toEqual([]);
    expect(limitErrors({ otpMaxSendsPerPhone: "20", otpMaxWrongCodesPerPhone: 50, otpLockMinutes: 1440 })).toEqual([]);
  });

  it("rejects values out of range or not whole", () => {
    expect(limitErrors({ otpMaxSendsPerPhone: 0 })).toEqual(["otpMaxSendsPerPhone"]);
    expect(limitErrors({ otpMaxSendsPerPhone: 21 })).toEqual(["otpMaxSendsPerPhone"]);
    expect(limitErrors({ otpMaxWrongCodesPerPhone: 51 })).toEqual(["otpMaxWrongCodesPerPhone"]);
    expect(limitErrors({ otpLockMinutes: 1441 })).toEqual(["otpLockMinutes"]);
    expect(limitErrors({ otpLockMinutes: 2.5 })).toEqual(["otpLockMinutes"]);
  });
});

describe("consent and data form", () => {
  const privacyErrors = (input: Record<string, unknown>) => {
    const result = privacySettingsSchema(3).safeParse({ policyVersion: 3, webhookPayloadRetentionDays: 90, ...input });
    return result.success ? [] : result.error.issues.map((issue) => issue.path.join("."));
  };

  it("lets the policy version stay or go up, never down", () => {
    expect(privacyErrors({ policyVersion: "4" })).toEqual([]);
    expect(privacyErrors({ policyVersion: 2 })).toEqual(["policyVersion"]);
  });

  it("keeps retention days within range", () => {
    expect(privacyErrors({ webhookPayloadRetentionDays: "3650" })).toEqual([]);
    expect(privacyErrors({ webhookPayloadRetentionDays: 0 })).toEqual(["webhookPayloadRetentionDays"]);
    expect(privacyErrors({ webhookPayloadRetentionDays: 3651 })).toEqual(["webhookPayloadRetentionDays"]);
  });
});

describe("resolve reconciliation item form", () => {
  it("needs a note within the length limit", () => {
    expect(resolveItemSchema.safeParse({ note: "Refunded in dashboard" }).success).toBe(true);
    expect(resolveItemSchema.safeParse({ note: "   " }).success).toBe(false);
    expect(resolveItemSchema.safeParse({ note: "a".repeat(501) }).success).toBe(false);
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

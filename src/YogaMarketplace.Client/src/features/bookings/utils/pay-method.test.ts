import { describe, expect, it } from "vitest";
import type { BookingQuote, CreditOption } from "@/features/bookings/types";
import {
  isLostCredit,
  payButtonLabel,
  resolvePayMethod,
  showCreditChoice,
} from "@/features/bookings/utils/pay-method";
import { ApiError } from "@/services/http/api-error";

function credit(overrides: Partial<CreditOption> = {}): CreditOption {
  return {
    available: true,
    reason: null,
    message: null,
    convenienceFee: 0,
    total: 0,
    batchId: "batch-1",
    packName: "8 sessions",
    useBy: "2026-11-10",
    ...overrides,
  };
}

function quote(option: CreditOption): BookingQuote {
  return {
    slotId: "slot-1",
    serviceId: "service-1",
    sessionAmount: 749,
    convenienceFee: 0,
    total: 749,
    currency: "INR",
    isTrial: false,
    credit: option,
  };
}

describe("pay method", () => {
  it("uses a credit by default when the server offers one, and lets the customer pay instead", () => {
    expect(resolvePayMethod(credit(), null)).toBe("credit");
    expect(resolvePayMethod(credit(), "pay")).toBe("pay");
  });

  it("pays when no credit is available, whatever was chosen before", () => {
    const none = credit({ available: false, reason: "NoCredits" });
    expect(resolvePayMethod(none, "credit")).toBe("pay");
    expect(resolvePayMethod(undefined, null)).toBe("pay");
  });

  it("explains a credit that doesn't cover the session, and hides it when there are none", () => {
    expect(showCreditChoice(credit())).toBe(true);
    expect(showCreditChoice(credit({ available: false, reason: "AboveCap" }))).toBe(true);
    expect(showCreditChoice(credit({ available: false, reason: "ExpiresBeforeSession" }))).toBe(true);
    expect(showCreditChoice(credit({ available: false, reason: "NoCredits" }))).toBe(false);
    expect(showCreditChoice(credit({ available: false, reason: "Trial" }))).toBe(false);
  });

  it("treats a 409 as a lost credit and anything else as a normal error", () => {
    expect(isLostCredit(new ApiError("Your last credit was just used on another booking.", 409))).toBe(true);
    expect(isLostCredit(new ApiError("Server error", 500))).toBe(false);
    expect(isLostCredit(new Error("boom"))).toBe(false);
  });

  it("labels the button with the server's amounts", () => {
    expect(payButtonLabel("credit", quote(credit()))).toBe("Book with 1 credit");
    expect(payButtonLabel("credit", quote(credit({ convenienceFee: 25, total: 25 })))).toBe("Use 1 credit and pay ₹25");
    expect(payButtonLabel("pay", quote(credit()))).toBe("Pay ₹749");
  });
});

import { describe, expect, it } from "vitest";
import type { CancelQuote } from "@/features/bookings/types";
import { cancelButtonLabel, creditOutcomeLabel, quoteNote } from "@/features/bookings/utils/cancel-copy";

const base: CancelQuote = {
  amount: 929,
  sessionAmount: 899,
  convenienceFee: 30,
  lateCancelFee: 0,
  convenienceFeeKept: 0,
  refund: 929,
  currency: "INR",
  lateCancelFeeType: "Percent",
  lateCancelFeeValue: 50,
  freeUntil: "2026-10-05T01:30:00Z",
  paidWithCredit: false,
  creditReturned: false,
};

const credit: CancelQuote = {
  ...base,
  amount: 0,
  sessionAmount: 0,
  convenienceFee: 0,
  refund: 0,
  paidWithCredit: true,
  creditReturned: true,
};

describe("credit booking cancel copy", () => {
  it("says the credit comes back on an early cancel, with the fee when one was paid", () => {
    expect(cancelButtonLabel(credit)).toBe("Cancel and get the credit back");
    expect(quoteNote(credit)).toMatch(/^Your credit comes back\./);
    expect(quoteNote({ ...credit, amount: 25, refund: 25 })).toMatch(/^Your credit and the ₹25 fee come back\./);
  });

  it("says the credit is used on a late cancel", () => {
    const late = { ...credit, creditReturned: false };
    expect(cancelButtonLabel(late)).toBe("Cancel and use the credit");
    expect(quoteNote(late)).toMatch(/credit counts as used/);
  });

  it("says no credit comes back when the pack was refunded, even before the late window", () => {
    const refunded = { ...credit, creditReturned: false, creditNotReturnedReason: "PackRefunded" as const };
    expect(quoteNote(refunded)).toBe("No credit will be returned (pack was refunded).");
    expect(cancelButtonLabel(refunded)).toBe("Cancel booking");
    expect(creditOutcomeLabel(refunded)).toBe("Not returned");
    expect(quoteNote({ ...refunded, amount: 25, refund: 25 })).toBe(
      "No credit will be returned (pack was refunded). Your ₹25 fee comes back.",
    );
  });

  it("says no credit comes back when the pack has expired", () => {
    const expired = { ...credit, creditReturned: false, creditNotReturnedReason: "PackExpired" as const };
    expect(quoteNote(expired)).toBe("No credit will be returned (pack has expired).");
  });

  it("keeps the refund wording for paid bookings", () => {
    expect(cancelButtonLabel(base)).toBe("Cancel and refund ₹929");
  });
});

describe("quoteNote", () => {
  it("says the convenience fee is not refunded on a late cancel", () => {
    const note = quoteNote({ ...base, lateCancelFee: 449.5, convenienceFeeKept: 30, refund: 449.5 });
    expect(note).toMatch(/convenience fee is not refunded on a late cancel/);
  });

  it("warns ahead of time that a late cancel keeps the convenience fee", () => {
    expect(quoteNote(base)).toMatch(/convenience fee is not refunded\.$/);
  });

  it("does not mention a convenience fee when there is none", () => {
    expect(quoteNote({ ...base, convenienceFee: 0 })).not.toMatch(/convenience/);
  });
});

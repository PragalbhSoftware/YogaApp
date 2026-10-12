import { describe, expect, it } from "vitest";
import type { CancelQuote } from "@/features/bookings/types";
import { quoteNote } from "@/features/bookings/utils/cancel-copy";

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
};

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

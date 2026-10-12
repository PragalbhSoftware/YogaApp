import type { CancelQuote } from "@/features/bookings/types";
import { formatWhenKolkata } from "@/utils/clock";
import { formatInr } from "@/utils/money";

export function quoteNote(quote: CancelQuote) {
  if (quote.lateCancelFee > 0 || quote.convenienceFeeKept > 0) {
    const closed = `The free cancellation window closed${
      quote.freeUntil ? ` at ${formatWhenKolkata(quote.freeUntil)}` : ""
    }. The late fee goes to your instructor, who kept this time for you.`;
    return quote.convenienceFeeKept > 0
      ? `${closed} The ${formatInr(quote.convenienceFeeKept)} convenience fee is not refunded on a late cancel.`
      : closed;
  }
  if (quote.freeUntil) {
    const after = `Free cancellation until ${formatWhenKolkata(quote.freeUntil)}. After that, a late fee of ${lateFeeText(quote)} is kept`;
    return quote.convenienceFee > 0
      ? `${after} and the ${formatInr(quote.convenienceFee)} convenience fee is not refunded.`
      : `${after}.`;
  }
  return "Your instructor hasn’t accepted yet, so cancelling is free.";
}

function lateFeeText(quote: CancelQuote) {
  return quote.lateCancelFeeType === "Flat"
    ? formatInr(Math.min(quote.lateCancelFeeValue, quote.sessionAmount))
    : `${quote.lateCancelFeeValue}% of the session`;
}

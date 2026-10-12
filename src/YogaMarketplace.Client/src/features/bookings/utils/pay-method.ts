import type { BookingQuote, CreditOption } from "@/features/bookings/types";
import { ApiError } from "@/services/http/api-error";
import { formatInr } from "@/utils/money";

export type PayMethod = "credit" | "pay";

/** Reasons worth showing next to a disabled credit option: the customer has credits, just not for this session. */
const explainedReasons: ReadonlyArray<CreditOption["reason"]> = ["AboveCap", "ExpiresBeforeSession"];

export function showCreditChoice(credit: CreditOption | undefined) {
  return Boolean(credit && (credit.available || explainedReasons.includes(credit.reason)));
}

/** A credit is used by default when the server offers one; the customer can switch to paying. */
export function resolvePayMethod(credit: CreditOption | undefined, choice: PayMethod | null): PayMethod {
  if (!credit?.available) return "pay";
  return choice ?? "credit";
}

/** A 409 on a credit booking means the credit went elsewhere (or no longer covers the slot): offer paying instead. */
export function isLostCredit(error: unknown) {
  return error instanceof ApiError && error.status === 409;
}

export function payButtonLabel(method: PayMethod, quote: BookingQuote | undefined) {
  if (method === "credit") {
    const fee = quote?.credit.total ?? 0;
    return fee > 0 ? `Use 1 credit and pay ${formatInr(fee)}` : "Book with 1 credit";
  }
  return quote ? `Pay ${formatInr(quote.total)}` : "Pay with Razorpay";
}

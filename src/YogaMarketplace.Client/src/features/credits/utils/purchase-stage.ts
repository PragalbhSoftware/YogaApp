import type { PackOrderStatus } from "@/features/credits/types";

export type PurchaseStage = "idle" | "confirming" | "slow" | "failed" | "done" | "refunded";

/** How long the packs page polls a paid order before asking the customer to check back later. */
export const packConfirmTimeoutMs = 2 * 60 * 1000;

/** How long an order may stay Failed, waiting for a late capture, before the customer is asked to pay again. */
export const packFailedTimeoutMs = 30 * 1000;

/**
 * Where a pack purchase stands. After Razorpay returns, the order stays "confirming" until the server reports it
 * Completed (credits exist) or Refunded; the client never shows credits before that. Past the polling window it is
 * "slow": still unconfirmed, and the customer must not pay again. If Razorpay reported it Failed by then, it is
 * "failed" and the customer can try again.
 */
export function purchaseStage(
  orderId: string | null,
  status: PackOrderStatus | null | undefined,
  timedOut = false,
): PurchaseStage {
  if (!orderId) return "idle";
  const current = status?.orderId === orderId ? status.status : null;
  if (current === "Completed") return "done";
  if (current === "Refunded") return "refunded";
  if (!timedOut) return "confirming";
  return current === "Failed" ? "failed" : "slow";
}

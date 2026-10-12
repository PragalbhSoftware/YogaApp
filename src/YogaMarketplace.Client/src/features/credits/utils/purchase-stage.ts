import type { PackOrderStatus } from "@/features/credits/types";

export type PurchaseStage = "idle" | "confirming" | "slow" | "done" | "refunded";

/** How long the packs page polls a paid order before asking the customer to check back later. */
export const packConfirmTimeoutMs = 2 * 60 * 1000;

/**
 * Where a pack purchase stands. After Razorpay returns, the order stays "confirming" until the server reports it
 * Completed (credits exist) or Refunded; the client never shows credits before that. Past the polling window it is
 * "slow": still unconfirmed, and the customer must not pay again.
 */
export function purchaseStage(
  orderId: string | null,
  status: PackOrderStatus | null | undefined,
  timedOut = false,
): PurchaseStage {
  if (!orderId) return "idle";
  if (status?.orderId === orderId && status.status === "Completed") return "done";
  if (status?.orderId === orderId && status.status === "Refunded") return "refunded";
  return timedOut ? "slow" : "confirming";
}

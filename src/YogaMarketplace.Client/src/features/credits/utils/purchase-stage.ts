import type { PackOrderStatus } from "@/features/credits/types";

export type PurchaseStage = "idle" | "confirming" | "done" | "refunded";

/**
 * Where a pack purchase stands. After Razorpay returns, the order stays "confirming" until the server reports it
 * Completed (credits exist) or Refunded; the client never shows credits before that.
 */
export function purchaseStage(orderId: string | null, status: PackOrderStatus | null | undefined): PurchaseStage {
  if (!orderId) return "idle";
  if (status?.orderId !== orderId) return "confirming";
  if (status.status === "Completed") return "done";
  if (status.status === "Refunded") return "refunded";
  return "confirming";
}

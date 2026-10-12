import { describe, expect, it } from "vitest";
import { purchaseStage } from "@/features/credits/utils/purchase-stage";

const status = (orderId: string, value: string) => ({ orderId, status: value, batch: null });

describe("purchaseStage", () => {
  it("is idle with no order", () => {
    expect(purchaseStage(null, status("order_1", "Completed"))).toBe("idle");
  });

  it("keeps confirming until the server reports this order captured", () => {
    expect(purchaseStage("order_1", undefined)).toBe("confirming");
    expect(purchaseStage("order_1", status("order_1", "Open"))).toBe("confirming");
    expect(purchaseStage("order_1", status("order_0", "Completed"))).toBe("confirming");
  });

  it("turns slow past the time limit unless the order already settled", () => {
    expect(purchaseStage("order_1", status("order_1", "Open"), true)).toBe("slow");
    expect(purchaseStage("order_1", undefined, true)).toBe("slow");
    expect(purchaseStage("order_1", status("order_1", "Completed"), true)).toBe("done");
  });

  it("finishes on Completed or Refunded", () => {
    expect(purchaseStage("order_1", status("order_1", "Completed"))).toBe("done");
    expect(purchaseStage("order_1", status("order_1", "Refunded"))).toBe("refunded");
  });
});

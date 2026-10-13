import { afterEach, describe, expect, it, vi } from "vitest";
import { openRazorpayCheckout } from "@/features/bookings/lib/razorpay-checkout";

const base = {
  keyId: "rzp_test_key",
  orderId: "order_1",
  amountPaise: 89900,
  currency: "INR",
  description: "Home session",
};

function captureOptions() {
  const seen: RazorpayCheckoutOptions[] = [];
  window.Razorpay = vi.fn(function (this: RazorpayInstance, options: RazorpayCheckoutOptions) {
    seen.push(options);
    this.open = () =>
      options.handler({ razorpay_payment_id: "pay_1", razorpay_order_id: "order_1", razorpay_signature: "sig" });
    this.on = () => undefined;
  }) as unknown as Window["Razorpay"];
  return seen;
}

describe("openRazorpayCheckout", () => {
  afterEach(() => {
    delete window.Razorpay;
  });

  it("prefills the customer's phone and locks it", async () => {
    const seen = captureOptions();
    await openRazorpayCheckout({ ...base, customerName: "Asha", customerPhone: "9714029497" });
    expect(seen[0].prefill?.contact).toBe("+919714029497");
    expect(seen[0].readonly).toEqual({ contact: true });
  });

  it("does not lock the contact when there is no phone", async () => {
    const seen = captureOptions();
    await openRazorpayCheckout({ ...base, customerPhone: "" });
    expect(seen[0].prefill?.contact).toBeUndefined();
    expect(seen[0].readonly).toBeUndefined();
  });
});

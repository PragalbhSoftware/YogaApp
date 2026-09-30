import { brand } from "@/constants/brand";

const SCRIPT_SRC = "https://checkout.razorpay.com/v1/checkout.js";

export class RazorpayCheckoutCancelled extends Error {
  constructor() {
    super("Payment cancelled. No booking was created.");
    this.name = "RazorpayCheckoutCancelled";
  }
}

export class RazorpayCheckoutFailed extends Error {
  constructor() {
    super("Payment did not go through. Try again.");
    this.name = "RazorpayCheckoutFailed";
  }
}

export type RazorpayCheckoutResult = {
  orderId: string;
  paymentId: string;
  signature: string;
};

export type OpenRazorpayCheckoutInput = {
  keyId: string;
  orderId: string;
  amountPaise: number;
  currency: string;
  description: string;
  customerName?: string | null;
  customerPhone?: string | null;
};

function loadCheckoutScript() {
  if (window.Razorpay) return Promise.resolve();

  const existing = document.querySelector<HTMLScriptElement>(`script[src="${SCRIPT_SRC}"]`);
  if (existing) {
    if (existing.dataset.failed === "true") {
      return Promise.reject(new Error("Could not load Razorpay."));
    }
    if (window.Razorpay || existing.dataset.loaded === "true") return Promise.resolve();
    return new Promise<void>((resolve, reject) => {
      existing.addEventListener("load", () => resolve(), { once: true });
      existing.addEventListener("error", () => reject(new Error("Could not load Razorpay.")), { once: true });
    });
  }

  return new Promise<void>((resolve, reject) => {
    const script = document.createElement("script");
    script.src = SCRIPT_SRC;
    script.async = true;
    script.onload = () => {
      script.dataset.loaded = "true";
      resolve();
    };
    script.onerror = () => {
      script.dataset.failed = "true";
      reject(new Error("Could not load Razorpay."));
    };
    document.body.appendChild(script);
  });
}

export async function openRazorpayCheckout(input: OpenRazorpayCheckoutInput): Promise<RazorpayCheckoutResult> {
  if (!input.keyId) {
    throw new Error("Razorpay is not configured.");
  }

  await loadCheckoutScript();
  const Razorpay = window.Razorpay;
  if (!Razorpay) {
    throw new Error("Could not load Razorpay.");
  }

  return new Promise((resolve, reject) => {
    let settled = false;
    const finish = (action: () => void) => {
      if (settled) return;
      settled = true;
      document.body.classList.remove("razorpay-checkout-open");
      action();
    };

    const checkout = new Razorpay({
      key: input.keyId,
      amount: input.amountPaise,
      currency: input.currency,
      name: "Yoga Marketplace",
      description: input.description,
      order_id: input.orderId,
      prefill: {
        name: input.customerName?.trim() || undefined,
        contact: input.customerPhone ? `+91${input.customerPhone.replace(/\D/g, "").slice(-10)}` : undefined,
      },
      theme: { color: brand.primary },
      handler: (response) => {
        finish(() =>
          resolve({
            orderId: response.razorpay_order_id || input.orderId,
            paymentId: response.razorpay_payment_id,
            signature: response.razorpay_signature,
          }),
        );
      },
      modal: {
        ondismiss: () => finish(() => reject(new RazorpayCheckoutCancelled())),
      },
    });

    checkout.on("payment.failed", () => finish(() => reject(new RazorpayCheckoutFailed())));
    document.body.classList.add("razorpay-checkout-open");
    checkout.open();
    window.requestAnimationFrame(forceCheckoutFrameVisible);
    window.setTimeout(forceCheckoutFrameVisible, 50);
    window.setTimeout(forceCheckoutFrameVisible, 300);
  });
}

function forceCheckoutFrameVisible() {
  const frame = document.querySelector<HTMLIFrameElement>("iframe.razorpay-checkout-frame");
  const container = document.querySelector<HTMLElement>(".razorpay-container");
  const backdrop = document.querySelector<HTMLElement>(".razorpay-backdrop");
  for (const node of [container, backdrop, frame]) {
    if (!node) continue;
    node.style.setProperty("z-index", "2147483646", "important");
    node.style.setProperty("opacity", "1", "important");
    node.style.setProperty("visibility", "visible", "important");
    node.style.setProperty("pointer-events", "auto", "important");
  }
  if (frame) {
    frame.style.setProperty("display", "block", "important");
    frame.style.setProperty("position", "fixed", "important");
    frame.style.setProperty("inset", "0px", "important");
    frame.style.setProperty("width", "100%", "important");
    frame.style.setProperty("height", "100%", "important");
    frame.style.setProperty("max-width", "none", "important");
    frame.style.setProperty("max-height", "none", "important");
  }
}

export type Pack = {
  id: string;
  name: string;
  sessions: number;
  price: number;
  perSession: number;
  validityDays: number;
  /** A credit covers any session up to this price; above it the customer pays normally. */
  priceCap: number;
  currency: string;
};

export type CreditBatch = {
  id: string;
  packName: string;
  sessions: number;
  remaining: number;
  priceCap: number;
  purchasedAt: string;
  expiresAt: string;
  /** Last India date a credit from this batch can be booked with (yyyy-MM-dd). */
  useBy: string;
};

/** Usable credits, earliest expiry first. */
export type MyCredits = {
  total: number;
  batches: CreditBatch[];
};

export type PackOrder = {
  checkoutId: string;
  keyId: string;
  orderId: string;
  amountPaise: number;
  amount: number;
  currency: string;
  packId: string;
  packName: string;
  localCapture: boolean;
};

export type PackOrderStatus = {
  orderId: string;
  /** Open until the server captures the payment; credits only exist once Completed. Failed: a payment attempt failed. */
  status: "Open" | "Failed" | "Completed" | "Refunded" | string;
  batch: CreditBatch | null;
};

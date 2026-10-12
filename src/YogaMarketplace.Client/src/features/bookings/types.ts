/** What the server will charge for a slot right now. The client never adds these up itself. */
export type BookingQuote = {
  slotId: string;
  serviceId: string;
  sessionAmount: number;
  convenienceFee: number;
  total: number;
  currency: string;
  isTrial: boolean;
  credit: CreditOption;
};

/**
 * The "Use 1 credit" option, priced by the server. When it isn't available, `message` says why and is safe to show.
 * `total` is what the customer pays on top of the credit (the convenience fee, when the pack's rules charge it).
 */
export type CreditOption = {
  available: boolean;
  reason: "NoCredits" | "AboveCap" | "ExpiresBeforeSession" | "Trial" | "Blocked" | null;
  message: string | null;
  convenienceFee: number;
  total: number;
  batchId: string | null;
  packName: string | null;
  useBy: string | null;
};

/** The signed-in customer's trial with one instructor. */
export type TrialStatus = {
  offered: boolean;
  price: number | null;
  used: boolean;
};

export type CheckoutOrder = {
  checkoutId: string;
  keyId: string;
  orderId: string;
  amountPaise: number;
  /** Session price plus the convenience fee: what the customer pays. */
  amount: number;
  sessionAmount: number;
  convenienceFee: number;
  currency: string;
  slotId: string;
  mode: string;
  providerName: string;
  localCapture: boolean;
};

export const bookingStatuses = [
  "PendingAccept",
  "Upcoming",
  "Declined",
  "Completed",
  "NoShow",
  "Cancelled",
] as const;

export type BookingStatus = (typeof bookingStatuses)[number];

export type BookingRecord = {
  id: string;
  providerId: string;
  providerName: string;
  serviceId: string;
  serviceTitle: string;
  slotId: string;
  mode: string;
  status: string;
  amount: number;
  convenienceFee: number;
  currency: string;
  date: string;
  start: string;
  end: string;
  homeAddress: string | null;
  landmark: string | null;
  meetLink: string | null;
  studioAddress: string | null;
  paymentStatus: string;
  gatewayOrderId: string | null;
  gatewayPaymentId: string | null;
  createdAt: string;
  hasReviewed: boolean;
  refundedAmount: number;
  lateCancelFee: number | null;
  cancelledBy: "Customer" | "Admin" | null;
  cancelReason: string | null;
  isTrial: boolean;
  paidWithCredit: boolean;
};

export type CancelQuote = {
  /** Everything the customer paid: session plus convenience fee. */
  amount: number;
  sessionAmount: number;
  convenienceFee: number;
  lateCancelFee: number;
  convenienceFeeKept: number;
  refund: number;
  currency: string;
  lateCancelFeeType: "Percent" | "Flat";
  lateCancelFeeValue: number;
  freeUntil: string | null;
  paidWithCredit: boolean;
  /** For a credit booking: whether cancelling now gives a usable credit back. */
  creditReturned: boolean;
  /** For a credit booking that won't get its credit back: why. */
  creditNotReturnedReason?: "LateCancel" | "PackRefunded" | "PackExpired" | null;
};

export type CreateOrderInput = {
  slotId: string;
  trial?: boolean;
  /** Pay only the convenience fee and use a credit for the session. */
  useCredit?: boolean;
  homeAddress?: string;
  landmark?: string;
};

export type ConfirmPaymentInput = {
  orderId: string;
  paymentId: string;
  signature: string;
};

export type CreateReviewInput = {
  rating: number;
  comment?: string;
};

export type ReviewRecord = {
  id: string;
  bookingId: string;
  providerId: string;
  rating: number;
  comment: string | null;
  createdAt: string;
};

export function isFakeOrder(orderId: string) {
  return orderId.startsWith("order_fake_");
}

export function isBookingStatus(value: string | null | undefined): value is BookingStatus {
  return Boolean(value && bookingStatuses.includes(value as BookingStatus));
}

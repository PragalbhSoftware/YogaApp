export const providerStatuses = ["Pending", "Verified", "Rejected"] as const;
export type ProviderStatus = (typeof providerStatuses)[number];

export const certificateStatuses = ["Pending", "Approved", "Rejected"] as const;
export type AdminCertificateStatus = (typeof certificateStatuses)[number];

export type AdminCertificate = {
  id: string;
  providerId: string;
  providerName: string;
  title: string;
  contentType: string;
  sizeBytes: number;
  status: AdminCertificateStatus;
  rejectionReason: string | null;
  createdAt: string;
  reviewedAt: string | null;
};

export const adminRoles = ["Customer", "Provider", "Admin"] as const;
export type AdminRole = (typeof adminRoles)[number];

export const paymentStatuses = ["Paid", "PartiallyRefunded", "Refunded", "Failed"] as const;
export type PaymentStatus = (typeof paymentStatuses)[number];

export const payoutStatuses = ["Pending", "Exported", "Paid"] as const;
export type PayoutStatus = (typeof payoutStatuses)[number];

export type AdminProvider = {
  id: string;
  userId: string;
  displayName: string;
  bio: string | null;
  age: number | null;
  email: string | null;
  phone: string;
  areaId: string;
  area: string;
  city: string;
  status: string;
  offersHome: boolean;
  offersStudio: boolean;
  offersOnline: boolean;
  homeRate: number | null;
  studioRate: number | null;
  onlineRate: number | null;
  studioAddress: string | null;
  googleMeetLink: string | null;
  rejectionReason: string | null;
  createdAt: string;
  reviewedAt: string | null;
};

export type AdminUserProvider = {
  id: string;
  displayName: string;
  status: string;
  area: string;
};

export type AdminUserSummary = {
  id: string;
  name: string | null;
  phone: string;
  gender: string | null;
  email: string | null;
  role: string;
  createdAt: string;
  provider: AdminUserProvider | null;
  isBlocked: boolean;
};

export type AdminUserBlockEvent = {
  id: string;
  action: "Blocked" | "Unblocked";
  reason: string | null;
  adminUserId: string;
  adminName: string | null;
  createdAt: string;
};

export type AdminUserDetail = {
  id: string;
  name: string | null;
  phone: string;
  gender: string | null;
  email: string | null;
  role: string;
  createdAt: string;
  provider: AdminProvider | null;
  isBlocked: boolean;
  blockedAt: string | null;
  blockedReason: string | null;
  blockHistory: AdminUserBlockEvent[];
};

export type AdminBooking = {
  id: string;
  customerId: string;
  customerName: string | null;
  customerPhone: string;
  providerId: string;
  providerName: string;
  serviceId: string;
  serviceTitle: string;
  slotId: string;
  mode: string;
  status: string;
  amount: number;
  convenienceFee: number;
  commissionPercent: number;
  currency: string;
  date: string;
  start: string;
  end: string;
  homeAddress: string | null;
  landmark: string | null;
  meetLink: string | null;
  studioAddress: string | null;
  paymentStatus: string | null;
  paymentId: string | null;
  gatewayOrderId: string | null;
  gatewayPaymentId: string | null;
  reviewRating: number | null;
  payoutNet: number | null;
  payoutStatus: string | null;
  createdAt: string;
  refundedAmount: number | null;
  lateCancelFee: number | null;
  cancelledBy: "Customer" | "Admin" | null;
  cancelReason: string | null;
  paidWithCredit: boolean;
  /** The pack's per-session value frozen at booking; null for paid bookings. */
  creditValue: number | null;
};

/** A booking payment, or a pack sale (no booking or provider; `packName` instead). */
export type AdminPayment = {
  id: string;
  kind: "Booking" | "Pack";
  bookingId: string | null;
  amount: number;
  currency: string;
  status: string;
  gateway: string | null;
  gatewayOrderId: string | null;
  gatewayPaymentId: string | null;
  createdAt: string;
  updatedAt: string | null;
  providerId: string | null;
  providerName: string | null;
  packName: string | null;
  customerId: string;
  customerName: string | null;
  customerPhone: string;
  refundedAmount: number;
};

export type AdminPayout = {
  id: string;
  bookingId: string;
  providerId: string;
  providerName: string;
  grossAmount: number;
  feePercent: number;
  feeAmount: number;
  netAmount: number;
  status: string;
  createdAt: string;
  bookingStatus: string;
};

export type AdminArea = {
  id: string;
  city: string;
  name: string;
  isActive: boolean;
};

export type AdminCategory = {
  id: string;
  name: string;
  slug: string;
  isActive: boolean;
};

export const lateCancelFeeTypes = ["Percent", "Flat"] as const;
export type LateCancelFeeType = (typeof lateCancelFeeTypes)[number];

export const payoutCycles = ["Weekly", "Biweekly"] as const;
export type PayoutCycle = (typeof payoutCycles)[number];

export const creditPayBases = ["PackPerSession", "SessionPrice"] as const;
export type CreditPayBasis = (typeof creditPayBases)[number];

export const creditLateCancelRules = ["PayInstructorShare", "KeepCredit", "ReturnCredit"] as const;
export type CreditLateCancel = (typeof creditLateCancelRules)[number];

/** Pack rules. Each purchase saves the values in force when it was bought. */
export type CreditSettings = {
  creditConvenienceFee: boolean;
  creditPayBasis: CreditPayBasis;
  creditLateCancel: CreditLateCancel;
  creditBookingStandsAfterExpiry: boolean;
  packRefundsEnabled: boolean;
  packReferenceSessionPrice: number;
  packRefundFee: number;
};

/** Daily Razorpay reconciliation. `reconciliationTime` is HH:mm on the market clock. */
export type ReconciliationSettings = {
  reconciliationTime: string;
  webhookStuckMinutes: number;
  refundPendingHours: number;
};

export type AdminSettings = CreditSettings & ReconciliationSettings & {
  version: number;
  currency: string;
  commissionPercent: number;
  convenienceFee: number;
  cancelFreeWindowHours: number;
  rescheduleFreeWindowHours: number;
  lateCancelFeeType: LateCancelFeeType;
  lateCancelFeeValue: number;
  payoutCycle: PayoutCycle;
  payoutPeriodStart: string;
  policyNote: string;
  bannerTitle: string | null;
  bannerSubtitle: string | null;
  bannerOffer: string | null;
  updatedAt: string | null;
};

/** Fields an admin can edit. Send only what changed, plus the version that was edited. */
export type UpdateSettingsInput = Partial<
  Pick<
    AdminSettings,
    | "commissionPercent"
    | "convenienceFee"
    | "cancelFreeWindowHours"
    | "lateCancelFeeType"
    | "lateCancelFeeValue"
    | "payoutCycle"
    | "policyNote"
    | keyof CreditSettings
    | keyof ReconciliationSettings
  >
> & {
  version: number;
  bannerTitle?: string;
  bannerSubtitle?: string;
  bannerOffer?: string;
};

export type AdminPack = {
  id: string;
  name: string;
  sessions: number;
  price: number;
  validityDays: number;
  priceCap: number;
  isActive: boolean;
  version: number;
  sold: number;
  createdAt: string;
  updatedAt: string | null;
};

export type CreatePackInput = Pick<AdminPack, "name" | "sessions" | "price" | "validityDays" | "priceCap">;

/** Send the version that was edited; omitted fields stay as they are. */
export type UpdatePackInput = Partial<CreatePackInput & { isActive: boolean }> & { version: number };

export const creditBatchStatuses = ["Active", "UsedUp", "Expired", "Refunded"] as const;
export type CreditBatchStatus = (typeof creditBatchStatuses)[number];

/** One pack purchase with the rules saved on it. `refundPreview` is what a partial refund would pay now. */
export type AdminCreditBatch = {
  id: string;
  packId: string;
  packName: string;
  sessions: number;
  remaining: number;
  used: number;
  price: number;
  priceCap: number;
  currency: string;
  purchasedAt: string;
  expiresAt: string;
  useBy: string;
  status: CreditBatchStatus;
  refundsEnabled: boolean;
  referenceSessionPrice: number;
  refundFee: number;
  refundPreview: number;
  refundedAmount: number;
  refundedAt: string | null;
  gatewayPaymentId: string;
  payBasis: CreditPayBasis;
  lateCancel: CreditLateCancel;
  bookingStandsAfterExpiry: boolean;
  chargeConvenienceFee: boolean;
};

export type SettingsAuditEntry = {
  id: string;
  changedAt: string;
  adminUserId: string;
  adminName: string | null;
  field: string;
  oldValue: string | null;
  newValue: string | null;
};

export type AdminReport = {
  bookingsByStatus: { status: string; count: number }[];
  /** Money paid for bookings, net of refunds. Bookings made with a credit never count. */
  gmvPaid: number;
  currency: string;
  pendingPayouts: { count: number; gross: number; net: number };
  /** Pack payments net of pack refunds. */
  packSales: number;
  /** Bookings plus packs, as calculated by the server. */
  revenue: number;
  paidBookings: number;
  creditBookings: number;
};

export const reconciliationItemTypes = [
  "MissingInDatabase",
  "MissingInRazorpay",
  "AmountMismatch",
  "StatusMismatch",
  "RefundMismatch",
  "RefundNotRecorded",
  "DashboardRefund",
  "StuckWebhook",
  "RefundPending",
] as const;
export type ReconciliationItemType = (typeof reconciliationItemTypes)[number];

export const reconciliationItemStatuses = ["Open", "Resolved"] as const;
export type ReconciliationItemStatus = (typeof reconciliationItemStatuses)[number];

/** One daily run. `gatewayCompared` is false when only the database checks ran (test gateway). */
export type ReconciliationRun = {
  id: string;
  marketDate: string;
  windowStart: string;
  windowEnd: string;
  startedAt: string;
  completedAt: string | null;
  status: "Completed" | "Failed";
  gatewayCompared: boolean;
  itemCount: number;
  openCount: number;
  error: string | null;
};

export type ReconciliationItem = {
  id: string;
  runId: string;
  marketDate: string;
  type: ReconciliationItemType;
  status: ReconciliationItemStatus;
  gatewayPaymentId: string | null;
  gatewayRefundId: string | null;
  reference: string | null;
  databaseValue: string | null;
  gatewayValue: string | null;
  detail: string;
  createdAt: string;
  resolvedAt: string | null;
  resolvedByName: string | null;
  resolutionNote: string | null;
};

export type ReconciliationItemQuery = {
  runId?: string;
  type?: ReconciliationItemType;
  status?: ReconciliationItemStatus;
};

export type BookingListQuery = {
  status?: string;
  from?: string;
  to?: string;
};

export type UserListQuery = {
  q?: string;
  role?: string;
};

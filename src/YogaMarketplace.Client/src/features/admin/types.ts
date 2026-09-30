export const providerStatuses = ["Pending", "Verified", "Rejected"] as const;
export type ProviderStatus = (typeof providerStatuses)[number];

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
};

export type AdminPayment = {
  id: string;
  bookingId: string;
  amount: number;
  currency: string;
  status: string;
  gateway: string | null;
  gatewayOrderId: string | null;
  gatewayPaymentId: string | null;
  createdAt: string;
  updatedAt: string | null;
  providerId: string;
  providerName: string;
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

export type AdminPolicy = {
  currency: string;
  platformFeePercent: number;
  cancelFreeWindowHours: number;
  rescheduleFreeWindowHours: number;
  lateCancelFeePercent: number;
  policyNote: string;
};

export type AdminReport = {
  bookingsByStatus: { status: string; count: number }[];
  gmvPaid: number;
  currency: string;
  pendingPayouts: { count: number; gross: number; net: number };
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

import type { BookingRecord } from "@/features/bookings/types";
import { slotHasStarted } from "@/utils/clock";
import { formatInr } from "@/utils/money";

const filterLabels: Record<string, string> = {
  PendingAccept: "Pending",
  Upcoming: "Upcoming",
  Declined: "Declined",
  Completed: "Completed",
  NoShow: "No-show",
  Cancelled: "Cancelled",
};

const details: Record<string, string> = {
  PendingAccept: "Waiting for the instructor",
  Upcoming: "Accepted. This session is upcoming.",
  Declined: "Declined. The payment was refunded.",
  Completed: "This session is complete.",
  NoShow: "Marked as a no-show.",
  Cancelled: "Cancelled.",
};

const paymentLabels: Record<string, string> = {
  Paid: "Paid",
  Refunded: "Refunded",
  PartiallyRefunded: "Partly refunded",
  Failed: "Failed",
  Pending: "Pending",
};

const payoutReasons: Record<string, string> = {
  Completed: "Completed session",
  NoShow: "No-show",
  Cancelled: "Late cancel",
};

export function payoutReasonLabel(bookingStatus: string) {
  return payoutReasons[bookingStatus] ?? "Session";
}

export function statusFilterLabel(status: string) {
  return filterLabels[status] ?? status;
}

export function paymentStatusLabel(status: string) {
  return paymentLabels[status] ?? status;
}

export function statusDetail(status: string) {
  return details[status] ?? status;
}

export function bookingDetail(booking: BookingRecord) {
  if (booking.status !== "Cancelled") return statusDetail(booking.status);
  if (booking.cancelledBy === "Admin") {
    return `Cancelled by Yoga Marketplace${booking.cancelReason ? `: ${booking.cancelReason}` : ""}. Full refund issued.`;
  }
  if (booking.lateCancelFee) {
    return `Cancelled late. ${formatInr(booking.refundedAmount)} refunded; ${formatInr(booking.lateCancelFee)} late-cancel fee kept.`;
  }
  return "Cancelled. Full refund issued.";
}

export function canCancel(booking: BookingRecord) {
  return (
    (booking.status === "PendingAccept" || booking.status === "Upcoming") &&
    !slotHasStarted(booking.date, booking.start)
  );
}

export function canReschedule(booking: BookingRecord) {
  return booking.status === "Upcoming";
}

export function canSubmitReview(booking: BookingRecord) {
  return booking.status === "Completed" && !booking.hasReviewed;
}

export function showReviewedNote(booking: BookingRecord) {
  return booking.status === "Completed" && booking.hasReviewed;
}

export function canAccept(status: string) {
  return status === "PendingAccept";
}

export function canComplete(status: string) {
  return status === "Upcoming";
}

export function canMarkNoShow(status: string) {
  return status === "Upcoming";
}

export function joinMeetHref(booking: BookingRecord) {
  if (booking.mode !== "Online" || booking.status !== "Upcoming") return null;
  const href = booking.meetLink?.trim();
  if (!href || !/^https?:\/\//i.test(href)) return null;
  return href;
}

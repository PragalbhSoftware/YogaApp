import type { AdminPayout } from "@/features/admin/types";

export function pageCount(total: number, pageSize: number) {
  return Math.max(1, Math.ceil(total / pageSize));
}

export function rangeLabel(total: number, page: number, pageSize: number, noun: string) {
  if (total === 0) return `No ${noun}`;
  const first = (page - 1) * pageSize + 1;
  const last = Math.min(page * pageSize, total);
  return `${first}–${last} of ${total.toLocaleString("en-IN")} ${noun}`;
}

export type StatusTone = "neutral" | "positive" | "attention" | "negative";

const statusTones: Record<string, StatusTone> = {
  Paid: "positive",
  Verified: "positive",
  Approved: "positive",
  Completed: "positive",
  Upcoming: "positive",
  Pending: "attention",
  PendingAccept: "attention",
  Exported: "attention",
  PartiallyRefunded: "attention",
  Refunded: "neutral",
  Cancelled: "neutral",
  Declined: "negative",
  NoShow: "negative",
  Rejected: "negative",
  Failed: "negative",
};

export function statusTone(status: string): StatusTone {
  return statusTones[status] ?? "neutral";
}

export function canMarkPaid(payout: Pick<AdminPayout, "status">) {
  return payout.status === "Pending" || payout.status === "Exported";
}

import { formatInr } from "@/utils/money";
import { formatSlotDay, formatTimeRange12 } from "@/utils/clock";
import type { AdminProvider } from "@/features/admin/types";

export function formatAdminWhen(value: string) {
  return new Intl.DateTimeFormat("en-IN", {
    timeZone: "Asia/Kolkata",
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(value));
}

export function formatSessionWhen(date: string, start: string, end: string) {
  return `${formatSlotDay(date)} · ${formatTimeRange12(start, end)}`;
}

export function displayName(name: string | null | undefined, phone: string) {
  const trimmed = name?.trim();
  return trimmed || phone;
}

export function httpHref(value: string | null | undefined) {
  if (!value) return null;
  try {
    const url = new URL(value);
    if (url.protocol !== "http:" && url.protocol !== "https:") return null;
    return url.toString();
  } catch {
    return null;
  }
}

export function offeredModes(provider: AdminProvider) {
  const modes: { mode: string; rate: number | null }[] = [];
  if (provider.offersHome) modes.push({ mode: "Home", rate: provider.homeRate });
  if (provider.offersStudio) modes.push({ mode: "Studio", rate: provider.studioRate });
  if (provider.offersOnline) modes.push({ mode: "Online", rate: provider.onlineRate });
  return modes;
}

export function modeRateLabel(mode: string, rate: number | null) {
  return rate == null ? mode : `${mode} ${formatInr(rate)}`;
}

export const listCap = "Lists show up to 100 rows.";

export function bookingStatusCount(rows: { status: string; count: number }[], status: string) {
  return rows.find((row) => row.status === status)?.count ?? 0;
}

export function sumPayoutFees(rows: { feeAmount: number }[]) {
  return rows.reduce((sum, row) => sum + row.feeAmount, 0);
}

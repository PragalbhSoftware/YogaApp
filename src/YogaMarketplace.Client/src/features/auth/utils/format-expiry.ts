import { marketTimeZone } from "@/utils/clock";

export function formatIstExpiry(iso: string) {
  return new Intl.DateTimeFormat("en-IN", {
    timeZone: marketTimeZone(),
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(iso));
}

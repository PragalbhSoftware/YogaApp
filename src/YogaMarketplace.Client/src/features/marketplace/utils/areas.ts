import type { Area } from "@/features/marketplace/types";

export const areaClosedTitle = "Not available in this area right now";
export const areaClosedDescription = "Bookings in this area are paused. Browse instructors in areas that are open.";

const same = (a: string, b: string) => a.localeCompare(b, "en", { sensitivity: "accent" }) === 0;

/** The API lists open areas only, so a saved pick missing from the list was closed by an admin. */
export function isAreaOpen(areas: Area[], city: string, areaName: string) {
  return areas.some((area) => same(area.city, city) && same(area.name, areaName));
}

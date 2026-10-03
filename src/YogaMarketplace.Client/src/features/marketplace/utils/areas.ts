import type { Area } from "@/features/marketplace/types";

const same = (a: string, b: string) => a.localeCompare(b, "en", { sensitivity: "accent" }) === 0;

/** The API lists open areas only, so a saved pick missing from the list was closed by an admin. */
export function isAreaOpen(areas: Area[], city: string, areaName: string) {
  return areas.some((area) => same(area.city, city) && same(area.name, areaName));
}

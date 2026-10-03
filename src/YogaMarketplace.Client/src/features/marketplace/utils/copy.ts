import { launch } from "@/constants/launch";
import { site } from "@/constants/site";
import type { ModeRate, SiteBanner } from "@/features/marketplace/types";

/** Admin banner text, falling back to the built-in copy for any field left empty. */
export function bannerCopy(banner: SiteBanner | undefined) {
  return {
    title: banner?.title?.trim() || site.tagline,
    subtitle: banner?.subtitle?.trim() || null,
    offer: banner?.offer?.trim() || launch.heroChip,
  };
}

function joinWords(items: string[]) {
  if (items.length === 0) return "";
  if (items.length === 1) return items[0];
  if (items.length === 2) return `${items[0]} and ${items[1]}`;
  return `${items.slice(0, -1).join(", ")}, and ${items[items.length - 1]}`;
}

export function instructorIntro(name: string, area: string, verified: boolean) {
  const role = verified ? "verified yoga instructor" : "yoga instructor";
  return `${name} is a ${role} in ${area}.`;
}

export function practiceSummary(bio: string | null | undefined) {
  const text = bio?.trim() ?? "";
  if (!text) return null;
  if (/^hatha and restorative yoga\.?\s*home sessions in bandra\.?$/i.test(text)) {
    return "Teaches Hatha yoga (slow, posture-focused) and restorative yoga (gentle and restful).";
  }
  return text;
}

export function sessionOfferLine(modes: ModeRate[] | null | undefined, area: string) {
  const names = (modes ?? []).map((item) => item.mode);
  if (names.length === 0) {
    return `Session types for ${area} will show here when this instructor lists them.`;
  }
  return `Book ${joinWords(names)} sessions in ${area}. Fees are per session.`;
}

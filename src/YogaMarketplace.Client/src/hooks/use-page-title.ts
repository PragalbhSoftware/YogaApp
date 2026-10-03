import { useEffect } from "react";
import { site } from "@/constants/site";

export function pageTitle(title?: string | null) {
  const trimmed = title?.trim();
  return trimmed ? `${trimmed} · ${site.name}` : site.name;
}

/** Sets `document.title`. Pass nothing (or null while data loads) for the plain product name. */
export function usePageTitle(title?: string | null) {
  useEffect(() => {
    document.title = pageTitle(title);
  }, [title]);
}

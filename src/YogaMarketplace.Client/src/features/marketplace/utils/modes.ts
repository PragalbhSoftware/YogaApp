import { sessionModes, type SessionMode } from "@/constants/catalog";
import type { ModeRate } from "@/features/marketplace/types";

export function offeredSessionModes(modes: ModeRate[] | null | undefined): SessionMode[] {
  const offered = new Set((modes ?? []).map((item) => item.mode));
  return sessionModes.filter((mode) => offered.has(mode));
}

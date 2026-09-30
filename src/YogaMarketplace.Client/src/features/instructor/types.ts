import type { BookingRecord } from "@/features/bookings/types";
import type { SessionMode } from "@/constants/catalog";

export type InstructorProfile = {
  id: string;
  userId: string;
  displayName: string;
  bio: string | null;
  age: number | null;
  email: string | null;
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
};

export type OwnedSlot = {
  id: string;
  mode: string;
  date: string;
  start: string;
  end: string;
  isBlocked: boolean;
};

export type OwnedSlotList = {
  mode: string;
  from: string;
  to: string;
  slots: OwnedSlot[] | null;
};

export type InstructorBooking = BookingRecord;

export type SlotState = "open" | "blocked" | "occupied" | "past";

export type RegisterInstructorInput = {
  displayName: string;
  age?: number;
  email?: string;
  areaId: string;
  bio?: string;
  offersHome: boolean;
  offersStudio: boolean;
  offersOnline: boolean;
  homeRate?: number;
  studioRate?: number;
  onlineRate?: number;
  studioAddress?: string;
  googleMeetLink?: string;
};

export type RegisterInstructorResult = {
  provider: InstructorProfile;
  token: string;
};

export type UpdateInstructorRatesInput = {
  homeRate?: number;
  studioRate?: number;
  onlineRate?: number;
};

export type UpdateInstructorProfileInput = RegisterInstructorInput;

export type InstructorPayout = {
  id: string;
  bookingId: string;
  grossAmount: number;
  feePercent: number;
  feeAmount: number;
  netAmount: number;
  status: string;
  createdAt: string;
  bookingStatus: string;
};

export type AddSlotInput = {
  mode: SessionMode;
  date: string;
  start: string;
  end: string;
};

export type UpdateSlotInput = {
  id: string;
  date: string;
  start: string;
  end: string;
};

const occupying = new Set(["PendingAccept", "Upcoming", "Completed", "NoShow"]);

export function bookingOccupiesSlot(status: string) {
  return occupying.has(status);
}

export function slotState(slot: OwnedSlot, occupied: boolean, today: string): SlotState {
  if (occupied) return "occupied";
  if (slot.isBlocked) return "blocked";
  if (slot.date < today) return "past";
  return "open";
}

export function offeredModes(profile: InstructorProfile): SessionMode[] {
  const modes: SessionMode[] = [];
  if (profile.offersHome) modes.push("Home");
  if (profile.offersStudio) modes.push("Studio");
  if (profile.offersOnline) modes.push("Online");
  return modes;
}

export function canManageSlots(profile: InstructorProfile) {
  return profile.status.toLowerCase() === "verified";
}

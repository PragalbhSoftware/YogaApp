import { create } from "zustand";
import type { HomeVisitFormValues } from "@/features/bookings/schemas";
import { useAuthStore } from "@/stores/auth-store";

type BookingDraft = {
  changingAddress: boolean;
  address: HomeVisitFormValues | null;
};

type BookingDraftState = {
  drafts: Record<string, BookingDraft>;
  saveDraft: (slotId: string, changes: Partial<BookingDraft>) => void;
  clearDraft: (slotId: string) => void;
  clearAll: () => void;
};

export const emptyBookingDraft: BookingDraft = { changingAddress: false, address: null };

/**
 * Unsaved checkout edits per slot, so a trip to a policy page and back keeps them. Memory only: the address is personal
 * data, so it is never written to browser storage and a full reload starts the form again.
 */
export const useBookingDraftStore = create<BookingDraftState>()((set) => ({
  drafts: {},
  saveDraft: (slotId, changes) =>
    set((state) => ({
      drafts: { ...state.drafts, [slotId]: { ...(state.drafts[slotId] ?? emptyBookingDraft), ...changes } },
    })),
  clearDraft: (slotId) =>
    set((state) => {
      const { [slotId]: _cleared, ...rest } = state.drafts;
      return { drafts: rest };
    }),
  clearAll: () => set({ drafts: {} }),
}));

useAuthStore.subscribe((state, previous) => {
  if (previous.token && !state.token) useBookingDraftStore.getState().clearAll();
});

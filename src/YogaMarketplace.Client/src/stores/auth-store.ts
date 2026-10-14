import { create } from "zustand";
import type { User } from "@/features/auth/types";

/** Where older builds persisted the session, token included. */
const LEGACY_STORAGE_KEY = "ym.auth";

type AuthState = {
  token: string | null;
  user: User | null;
  signIn: (token: string, user: User) => void;
  signOut: () => void;
};

/**
 * Memory only, so a script injected into the page can't read a stored token. A reload restores the session
 * from the httpOnly refresh cookie (see `useRestoredSession`).
 */
export const useAuthStore = create<AuthState>()((set) => ({
  token: null,
  user: null,
  signIn: (token, user) => set({ token, user }),
  signOut: () => set({ token: null, user: null }),
}));

/** Drops a session an older build left in localStorage. */
export function forgetStoredSession() {
  try {
    localStorage.removeItem(LEGACY_STORAGE_KEY);
  } catch {
    // Storage blocked: nothing could have been stored either.
  }
}

import { create } from "zustand";
import type { User } from "@/features/auth/types";
import { useCityStore } from "@/stores/city-store";

/** Where older builds persisted the session, token included. */
const LEGACY_STORAGE_KEY = "ym.auth";
/** Not a secret: only says this browser signed in, so a guest's page load can skip the refresh call. */
const SIGNED_IN_KEY = "ym.signedIn";

function rememberSignedIn(signedIn: boolean) {
  try {
    if (signedIn) localStorage.setItem(SIGNED_IN_KEY, "1");
    else localStorage.removeItem(SIGNED_IN_KEY);
  } catch {
    // Storage blocked: wasSignedIn() then always tries the refresh.
  }
}

/** True when this browser may hold a refresh cookie. Blocked storage counts as yes, so nobody is signed out by it. */
export function wasSignedIn() {
  try {
    return localStorage.getItem(SIGNED_IN_KEY) === "1";
  } catch {
    return true;
  }
}

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
  signIn: (token, user) => {
    rememberSignedIn(true);
    set({ token, user });
  },
  signOut: () => {
    rememberSignedIn(false);
    useCityStore.getState().clearPreferredCitySyncPending();
    set({ token: null, user: null });
  },
}));

/** Drops a session an older build left in localStorage. That browser signed in, so it gets the flag first. */
export function forgetStoredSession() {
  try {
    if (localStorage.getItem(LEGACY_STORAGE_KEY) === null) return;
    localStorage.setItem(SIGNED_IN_KEY, "1");
    localStorage.removeItem(LEGACY_STORAGE_KEY);
  } catch {
    // Storage blocked: nothing could have been stored either.
  }
}

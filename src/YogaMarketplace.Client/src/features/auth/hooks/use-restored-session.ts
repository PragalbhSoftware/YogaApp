import { useEffect, useState } from "react";
import { ApiError } from "@/services/http/api-error";
import { refreshSession } from "@/services/http/http-client";
import { forgetStoredSession, useAuthStore, wasSignedIn } from "@/stores/auth-store";

/** A slow refresh must not hold the whole app on the loading screen; past this the visitor starts signed out. */
export const SESSION_RESTORE_TIMEOUT_MS = 5_000;

/**
 * Rebuilds the in-memory session from the refresh cookie once per page load. False until that answer is in. A browser
 * that never signed in has no cookie, so it skips the call and starts as a guest at once.
 */
export function useRestoredSession() {
  const [restored, setRestored] = useState(() => !wasSignedIn());
  useEffect(() => {
    forgetStoredSession();
    if (!wasSignedIn()) return;
    void refreshSession(SESSION_RESTORE_TIMEOUT_MS)
      .then((result) => {
        // The cookie is gone or refused; an unreachable server keeps the flag so the next load tries again.
        if (!result.ok && result.error instanceof ApiError && (result.error.status === 401 || result.error.status === 403))
          useAuthStore.getState().signOut();
      })
      .finally(() => setRestored(true));
  }, []);
  return restored;
}

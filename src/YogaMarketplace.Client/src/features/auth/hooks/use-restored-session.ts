import { useEffect, useState } from "react";
import { refreshSession } from "@/services/http/http-client";
import { forgetStoredSession } from "@/stores/auth-store";

/** A slow refresh must not hold the whole app on the loading screen; past this the visitor starts signed out. */
export const SESSION_RESTORE_TIMEOUT_MS = 5_000;

/** Rebuilds the in-memory session from the refresh cookie once per page load. False until that answer is in. */
export function useRestoredSession() {
  const [restored, setRestored] = useState(false);
  useEffect(() => {
    forgetStoredSession();
    void refreshSession(SESSION_RESTORE_TIMEOUT_MS).finally(() => setRestored(true));
  }, []);
  return restored;
}

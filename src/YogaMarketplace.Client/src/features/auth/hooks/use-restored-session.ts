import { useEffect, useState } from "react";
import { refreshSession } from "@/services/http/http-client";
import { forgetStoredSession } from "@/stores/auth-store";

/** Rebuilds the in-memory session from the refresh cookie once per page load. False until that answer is in. */
export function useRestoredSession() {
  const [restored, setRestored] = useState(false);
  useEffect(() => {
    forgetStoredSession();
    void refreshSession().finally(() => setRestored(true));
  }, []);
  return restored;
}

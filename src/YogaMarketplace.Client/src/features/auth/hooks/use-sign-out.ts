import { useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { routes } from "@/constants/routes";
import { authApi } from "@/features/auth/api/auth-api";
import { useAuthStore } from "@/stores/auth-store";

/** Revokes the refresh cookie on the server, then clears the local session. */
export function useSignOut() {
  const navigate = useNavigate();
  return useCallback(() => {
    void authApi.logout().catch(() => undefined);
    useAuthStore.getState().signOut();
    navigate(routes.login, { replace: true });
  }, [navigate]);
}

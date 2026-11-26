import { useEffect } from "react";
import type { QueryClient } from "@tanstack/react-query";
import { useAreaStore } from "@/stores/area-store";
import { useAuthStore } from "@/stores/auth-store";
import { useCityStore } from "@/stores/city-store";

/** Clears React Query and persisted city/area when the signed-in user becomes null (logout or expiry). */
export function useClearSessionOnSignOut(queryClient: QueryClient) {
  useEffect(
    () =>
      useAuthStore.subscribe((state, previous) => {
        if (previous.user && !state.user) {
          queryClient.clear();
          useCityStore.getState().clearCity();
          useAreaStore.getState().clearArea();
        }
      }),
    [queryClient],
  );
}

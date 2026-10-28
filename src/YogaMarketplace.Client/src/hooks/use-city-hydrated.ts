import { useSyncExternalStore } from "react";
import { useCityStore } from "@/stores/city-store";

export function useCityHydrated() {
  return useSyncExternalStore(
    (onChange) => useCityStore.persist.onFinishHydration(onChange),
    () => useCityStore.persist.hasHydrated(),
    () => false,
  );
}

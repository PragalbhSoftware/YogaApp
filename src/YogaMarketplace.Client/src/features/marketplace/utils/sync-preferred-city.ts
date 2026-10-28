import type { QueryClient } from "@tanstack/react-query";
import { clearPreferredCityOnProfile } from "@/features/marketplace/utils/choose-city";
import { profileApi } from "@/features/profile/api/profile-api";
import { profileQueryKey } from "@/features/profile/hooks/use-profile";
import { useAreaStore } from "@/stores/area-store";
import { useCityStore } from "@/stores/city-store";

type ActiveCity = { id: string };

/**
 * Aligns the browser city with the signed-in profile.
 * Profile wins when both are set and differ, unless a failed switch left a pending push for this user.
 * An inactive profile city is cleared.
 */
export async function syncPreferredCity(
  preferredCityId: string | null,
  cities: ActiveCity[],
  browserCityId: string | null,
  userId: string,
  queryClient: QueryClient,
): Promise<void> {
  const store = useCityStore.getState();
  const pending = store.preferredCitySyncPending;
  if (pending && pending !== userId) {
    store.clearPreferredCitySyncPending();
  }

  const active = preferredCityId && cities.some((city) => city.id === preferredCityId) ? preferredCityId : null;
  const pendingForUser = useCityStore.getState().preferredCitySyncPending === userId;

  if (pendingForUser && browserCityId && cities.some((city) => city.id === browserCityId)) {
    try {
      const profile = await profileApi.savePreferredCity(browserCityId);
      useCityStore.getState().clearPreferredCitySyncPending();
      queryClient.setQueryData(profileQueryKey, profile);
    } catch {
      // Keep pending; next login tries again. Local city stays.
    }
    return;
  }

  if (preferredCityId && !active) {
    useCityStore.getState().clearCity();
    useAreaStore.getState().clearArea();
    await clearPreferredCityOnProfile(queryClient);
    return;
  }

  if (active) {
    if (browserCityId !== active) {
      useAreaStore.getState().clearArea();
      useCityStore.getState().setCityId(active);
    }
    useCityStore.getState().clearPreferredCitySyncPending();
    return;
  }

  if (browserCityId && cities.some((city) => city.id === browserCityId)) {
    try {
      const profile = await profileApi.savePreferredCity(browserCityId);
      useCityStore.getState().clearPreferredCitySyncPending();
      queryClient.setQueryData(profileQueryKey, profile);
    } catch {
      useCityStore.getState().markPreferredCitySyncPending(userId);
    }
  }
}

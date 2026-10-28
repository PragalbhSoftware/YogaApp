import { clearPreferredCityOnProfile, chooseCity } from "@/features/marketplace/utils/choose-city";
import { profileApi } from "@/features/profile/api/profile-api";
import { useAreaStore } from "@/stores/area-store";
import { useCityStore } from "@/stores/city-store";

type ActiveCity = { id: string };

/**
 * Aligns the browser city with the signed-in profile.
 * Profile wins when both are set and differ, unless a failed switch left a pending push.
 * An inactive profile city is cleared.
 */
export async function syncPreferredCity(
  preferredCityId: string | null,
  cities: ActiveCity[],
  browserCityId: string | null,
): Promise<void> {
  const store = useCityStore.getState();
  const pending = store.preferredCitySyncPending;
  const active = preferredCityId && cities.some((city) => city.id === preferredCityId) ? preferredCityId : null;

  if (pending && browserCityId && cities.some((city) => city.id === browserCityId)) {
    try {
      await profileApi.savePreferredCity(browserCityId);
      store.clearPreferredCitySyncPending();
    } catch {
      // Keep pending; next login tries again. Local city stays.
    }
    return;
  }

  if (preferredCityId && !active) {
    useCityStore.getState().clearCity();
    useAreaStore.getState().clearArea();
    await clearPreferredCityOnProfile();
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
      await profileApi.savePreferredCity(browserCityId);
      useCityStore.getState().clearPreferredCitySyncPending();
    } catch {
      useCityStore.getState().markPreferredCitySyncPending();
    }
  }
}

export { chooseCity };

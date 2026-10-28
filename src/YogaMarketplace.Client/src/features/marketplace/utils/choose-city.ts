import { toast } from "sonner";
import { profileApi } from "@/features/profile/api/profile-api";
import { useAreaStore } from "@/stores/area-store";
import { useAuthStore } from "@/stores/auth-store";
import { useCityStore } from "@/stores/city-store";
import { toUserMessage } from "@/services/http/api-error";

/** Updates the browser city, clears the neighbourhood, and best-effort syncs the signed-in profile. */
export async function chooseCity(cityId: string): Promise<void> {
  const previous = useCityStore.getState().cityId;
  if (previous !== cityId) {
    useAreaStore.getState().clearArea();
  }
  useCityStore.getState().setCityId(cityId);

  if (!useAuthStore.getState().token) return;
  try {
    await profileApi.savePreferredCity(cityId);
    useCityStore.getState().clearPreferredCitySyncPending();
  } catch (error) {
    useCityStore.getState().markPreferredCitySyncPending();
    toast.error(toUserMessage(error));
  }
}

/** Clears a preferred city on the profile when the stored id is no longer active. */
export async function clearPreferredCityOnProfile(): Promise<void> {
  if (!useAuthStore.getState().token) return;
  try {
    await profileApi.savePreferredCity(null);
    useCityStore.getState().clearPreferredCitySyncPending();
  } catch (error) {
    toast.error(toUserMessage(error));
  }
}

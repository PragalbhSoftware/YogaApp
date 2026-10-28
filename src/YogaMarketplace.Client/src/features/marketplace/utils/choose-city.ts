import type { QueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { profileApi } from "@/features/profile/api/profile-api";
import { profileQueryKey } from "@/features/profile/hooks/use-profile";
import { useAreaStore } from "@/stores/area-store";
import { useAuthStore } from "@/stores/auth-store";
import { useCityStore } from "@/stores/city-store";
import { toUserMessage } from "@/services/http/api-error";

/** Updates the browser city, clears the neighbourhood, and best-effort syncs the signed-in profile. */
export async function chooseCity(cityId: string, queryClient: QueryClient): Promise<void> {
  const previous = useCityStore.getState().cityId;
  if (previous !== cityId) {
    useAreaStore.getState().clearArea();
  }
  useCityStore.getState().setCityId(cityId);

  const { token, user } = useAuthStore.getState();
  if (!token || !user) return;
  try {
    const profile = await profileApi.savePreferredCity(cityId);
    useCityStore.getState().clearPreferredCitySyncPending();
    queryClient.setQueryData(profileQueryKey, profile);
  } catch (error) {
    useCityStore.getState().markPreferredCitySyncPending(user.id);
    toast.error(toUserMessage(error));
  }
}

/** Clears a preferred city on the profile when the stored id is no longer active. */
export async function clearPreferredCityOnProfile(queryClient: QueryClient): Promise<void> {
  if (!useAuthStore.getState().token) return;
  try {
    const profile = await profileApi.savePreferredCity(null);
    useCityStore.getState().clearPreferredCitySyncPending();
    queryClient.setQueryData(profileQueryKey, profile);
  } catch (error) {
    toast.error(toUserMessage(error));
  }
}

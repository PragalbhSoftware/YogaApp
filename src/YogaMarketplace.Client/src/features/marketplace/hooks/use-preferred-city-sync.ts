import { useEffect, useRef } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/features/auth/hooks/use-auth";
import { useCities } from "@/features/marketplace/hooks/use-cities";
import { syncPreferredCity } from "@/features/marketplace/utils/sync-preferred-city";
import { useCustomerProfile } from "@/features/profile/hooks/use-profile";
import { useCityHydrated } from "@/hooks/use-city-hydrated";
import { useCityStore } from "@/stores/city-store";

/** Runs once per signed-in session after the profile and city list are ready. Guests are left alone. */
export function usePreferredCitySync() {
  const { isAuthenticated, user } = useAuth();
  const hydrated = useCityHydrated();
  const cityId = useCityStore((state) => state.cityId);
  const cities = useCities();
  const profile = useCustomerProfile();
  const queryClient = useQueryClient();
  const ranForUser = useRef<string | null>(null);

  useEffect(() => {
    if (!isAuthenticated || !user) {
      ranForUser.current = null;
      return;
    }
    if (!hydrated || !cities.isSuccess || !profile.isSuccess) return;
    if (ranForUser.current === user.id) return;
    ranForUser.current = user.id;
    void syncPreferredCity(profile.data.preferredCityId, cities.data, cityId, user.id, queryClient);
  }, [isAuthenticated, user, hydrated, cities.isSuccess, cities.data, profile.isSuccess, profile.data, cityId, queryClient]);
}

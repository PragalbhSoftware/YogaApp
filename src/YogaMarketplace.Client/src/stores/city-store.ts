import { create } from "zustand";
import { persist } from "zustand/middleware";

type CityState = {
  cityId: string | null;
  /** True when a signed-in city switch saved locally but the profile PUT failed — push on next login. */
  preferredCitySyncPending: boolean;
  setCityId: (cityId: string) => void;
  clearCity: () => void;
  markPreferredCitySyncPending: () => void;
  clearPreferredCitySyncPending: () => void;
};

type PersistedCity = Pick<CityState, "cityId" | "preferredCitySyncPending">;

export const useCityStore = create<CityState>()(
  persist(
    (set) => ({
      cityId: null,
      preferredCitySyncPending: false,
      setCityId: (cityId) => set({ cityId }),
      clearCity: () => set({ cityId: null }),
      markPreferredCitySyncPending: () => set({ preferredCitySyncPending: true }),
      clearPreferredCitySyncPending: () => set({ preferredCitySyncPending: false }),
    }),
    {
      name: "ym.city",
      version: 2,
      partialize: (state): PersistedCity => ({
        cityId: state.cityId,
        preferredCitySyncPending: state.preferredCitySyncPending,
      }),
    },
  ),
);

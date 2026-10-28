import { create } from "zustand";
import { persist } from "zustand/middleware";

type CityState = {
  cityId: string | null;
  /** Signed-in user id whose preferred-city PUT failed — push on that user's next login only. */
  preferredCitySyncPending: string | null;
  setCityId: (cityId: string) => void;
  clearCity: () => void;
  markPreferredCitySyncPending: (userId: string) => void;
  clearPreferredCitySyncPending: () => void;
};

type PersistedCity = Pick<CityState, "cityId" | "preferredCitySyncPending">;

export const useCityStore = create<CityState>()(
  persist(
    (set, get) => ({
      cityId: null,
      preferredCitySyncPending: null,
      setCityId: (cityId) => set({ cityId }),
      clearCity: () => set({ cityId: null }),
      markPreferredCitySyncPending: (userId) => set({ preferredCitySyncPending: userId }),
      clearPreferredCitySyncPending: () => {
        if (get().preferredCitySyncPending === null) return;
        set({ preferredCitySyncPending: null });
      },
    }),
    {
      name: "ym.city",
      version: 1,
      partialize: (state): PersistedCity => ({
        cityId: state.cityId,
        preferredCitySyncPending: state.preferredCitySyncPending,
      }),
    },
  ),
);

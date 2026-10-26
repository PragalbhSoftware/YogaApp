import { create } from "zustand";
import { persist } from "zustand/middleware";

type CityState = {
  cityId: string | null;
  setCityId: (cityId: string) => void;
  clearCity: () => void;
};

type PersistedCity = Pick<CityState, "cityId">;

export const useCityStore = create<CityState>()(
  persist(
    (set) => ({
      cityId: null,
      setCityId: (cityId) => set({ cityId }),
      clearCity: () => set({ cityId: null }),
    }),
    {
      name: "ym.city",
      version: 1,
      partialize: (state): PersistedCity => ({ cityId: state.cityId }),
    },
  ),
);

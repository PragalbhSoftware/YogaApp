import { create } from "zustand";
import { persist } from "zustand/middleware";
import { launch } from "@/constants/launch";

type AreaState = {
  city: string | null;
  areaName: string | null;
  setArea: (city: string, areaName: string) => void;
  clearArea: () => void;
};

type PersistedArea = Pick<AreaState, "city" | "areaName">;

export const useAreaStore = create<AreaState>()(
  persist(
    (set) => ({
      city: null,
      areaName: null,
      setArea: (city, areaName) => set({ city, areaName }),
      clearArea: () => set({ city: null, areaName: null }),
    }),
    {
      name: "ym.area",
      version: 1,
      partialize: (state): PersistedArea => ({ city: state.city, areaName: state.areaName }),
      migrate: (persisted, version) => {
        const previous = (persisted ?? {}) as Partial<PersistedArea>;
        if (version < 1) {
          const areaName = previous.areaName ?? null;
          return { areaName, city: areaName ? launch.firstCity : null };
        }
        return { city: previous.city ?? null, areaName: previous.areaName ?? null };
      },
    },
  ),
);

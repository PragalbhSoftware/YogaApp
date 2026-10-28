import { create } from "zustand";
import { persist } from "zustand/middleware";

type AreaState = {
  areaName: string | null;
  setArea: (areaName: string) => void;
  clearArea: () => void;
};

type PersistedArea = Pick<AreaState, "areaName">;

/**
 * Older saves stored a city name and no city id. That name is not a city id, so the customer picks again.
 * Version 0 stored only a neighbourhood name, which is ambiguous without a city.
 */
export function migrateArea(persisted: unknown, version: number): PersistedArea {
  const previous = (persisted ?? {}) as { city?: string | null; cityId?: string | null; areaName?: string | null };
  if (version < 1 || (Boolean(previous.city) && !previous.cityId) || !previous.areaName) {
    return { areaName: null };
  }
  return { areaName: previous.areaName };
}

export const useAreaStore = create<AreaState>()(
  persist(
    (set) => ({
      areaName: null,
      setArea: (areaName) => set({ areaName }),
      clearArea: () => set({ areaName: null }),
    }),
    {
      name: "ym.area",
      version: 2,
      partialize: (state): PersistedArea => ({ areaName: state.areaName }),
      migrate: migrateArea,
    },
  ),
);

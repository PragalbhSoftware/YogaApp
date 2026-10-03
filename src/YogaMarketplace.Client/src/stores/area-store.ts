import { create } from "zustand";
import { persist } from "zustand/middleware";

type AreaState = {
  city: string | null;
  areaName: string | null;
  setArea: (city: string, areaName: string) => void;
  clearArea: () => void;
};

type PersistedArea = Pick<AreaState, "city" | "areaName">;

/** Version 0 saved only a neighbourhood name. Without its city it is ambiguous, so the customer picks again. */
export function migrateArea(persisted: unknown, version: number): PersistedArea {
  const previous = (persisted ?? {}) as Partial<PersistedArea>;
  if (version < 1 || !previous.city || !previous.areaName) return { city: null, areaName: null };
  return { city: previous.city, areaName: previous.areaName };
}

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
      migrate: migrateArea,
    },
  ),
);

import { useEffect } from "react";
import { toast } from "sonner";
import { useAreas } from "@/features/marketplace/hooks/use-areas";
import { isAreaOpen } from "@/features/marketplace/utils/areas";
import { useAreaStore } from "@/stores/area-store";

/** Clears the saved area once the open-area list shows it was closed, so the customer picks again. */
export function useClosedAreaReset(city: string | null, areaName: string | null) {
  const areas = useAreas();
  const clearArea = useAreaStore((state) => state.clearArea);

  useEffect(() => {
    if (!areas.isSuccess || !city || !areaName) return;
    if (isAreaOpen(areas.data, city, areaName)) return;
    clearArea();
    toast.message(`${areaName} isn’t available right now. Pick another area.`);
  }, [areas.isSuccess, areas.data, city, areaName, clearArea]);
}

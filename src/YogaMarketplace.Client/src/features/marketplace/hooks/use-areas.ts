import { useQuery } from "@tanstack/react-query";
import { catalogApi } from "@/features/marketplace/api/catalog-api";

export function useAreas(cityId: string | null) {
  return useQuery({
    queryKey: ["areas", cityId],
    queryFn: () => catalogApi.areas(cityId ?? ""),
    enabled: Boolean(cityId),
  });
}

import { useQuery } from "@tanstack/react-query";
import { catalogApi } from "@/features/marketplace/api/catalog-api";

export function useAreas() {
  return useQuery({
    queryKey: ["areas"],
    queryFn: catalogApi.areas,
  });
}

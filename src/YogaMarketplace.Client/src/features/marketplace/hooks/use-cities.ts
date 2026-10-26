import { useQuery } from "@tanstack/react-query";
import { catalogApi } from "@/features/marketplace/api/catalog-api";

export function useCities() {
  return useQuery({
    queryKey: ["catalog-cities"],
    queryFn: catalogApi.cities,
  });
}

import { useQuery } from "@tanstack/react-query";
import { catalogApi } from "@/features/marketplace/api/catalog-api";

export function usePolicy() {
  return useQuery({
    queryKey: ["policy"],
    queryFn: catalogApi.policy,
  });
}

export function useSiteBanner() {
  return useQuery({
    queryKey: ["site-banner"],
    queryFn: catalogApi.banner,
  });
}

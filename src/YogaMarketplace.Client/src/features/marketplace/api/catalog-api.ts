import { http } from "@/services/http/http-client";
import type { Area, CatalogCity, PublicPolicy, SiteBanner } from "@/features/marketplace/types";

export const catalogApi = {
  areas(cityId: string) {
    return http.get<Area[]>("/api/areas", { params: { cityId } }).then((res) => res.data);
  },

  cities() {
    return http.get<CatalogCity[]>("/api/catalog/cities").then((res) => res.data);
  },

  nearestCity(lat: number, lng: number) {
    return http.get<CatalogCity | "">("/api/catalog/cities/nearest", { params: { lat, lng } }).then((res) => {
      if (res.status === 204 || !res.data) return null;
      return res.data;
    });
  },

  policy() {
    return http.get<PublicPolicy>("/api/policy").then((res) => res.data);
  },

  banner() {
    return http.get<SiteBanner>("/api/site/banner").then((res) => res.data);
  },
};

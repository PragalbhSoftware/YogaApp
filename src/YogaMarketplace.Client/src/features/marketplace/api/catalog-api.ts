import { http } from "@/services/http/http-client";
import type { Area, PublicPolicy, SiteBanner } from "@/features/marketplace/types";

export const catalogApi = {
  areas() {
    return http.get<Area[]>("/api/areas").then((res) => res.data);
  },

  policy() {
    return http.get<PublicPolicy>("/api/policy").then((res) => res.data);
  },

  banner() {
    return http.get<SiteBanner>("/api/site/banner").then((res) => res.data);
  },
};

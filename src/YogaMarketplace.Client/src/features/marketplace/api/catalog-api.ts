import { http } from "@/services/http/http-client";
import type { Area } from "@/features/marketplace/types";

export const catalogApi = {
  areas() {
    return http.get<Area[]>("/api/areas").then((res) => res.data);
  },
};

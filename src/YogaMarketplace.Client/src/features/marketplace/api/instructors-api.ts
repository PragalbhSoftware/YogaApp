import { http } from "@/services/http/http-client";
import { categorySlug, type SessionMode, type SessionModeFilter } from "@/constants/catalog";
import type { InstructorDetail, InstructorSummary, OpenSlotList, PublicReview } from "@/features/marketplace/types";

export const instructorsApi = {
  browse(cityId: string, areaId: string | null, mode: SessionModeFilter, page?: number, pageSize?: number) {
    return http
      .get<InstructorSummary[]>("/api/providers", {
        params: {
          cityId,
          ...(areaId ? { areaId } : {}),
          category: categorySlug,
          ...(mode ? { mode } : {}),
          ...(page ? { page } : {}),
          ...(pageSize ? { pageSize } : {}),
        },
      })
      .then((res) => res.data);
  },

  get(id: string) {
    return http.get<InstructorDetail>(`/api/providers/${id}`).then((res) => res.data);
  },

  reviews(id: string) {
    return http.get<PublicReview[]>(`/api/providers/${id}/reviews`).then((res) => res.data);
  },

  slots(id: string, mode: SessionMode, from: string, to: string) {
    return http
      .get<OpenSlotList>(`/api/providers/${id}/slots`, { params: { mode, from, to } })
      .then((res) => res.data);
  },
};

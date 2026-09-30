import { http } from "@/services/http/http-client";
import { categorySlug, type SessionMode, type SessionModeFilter } from "@/constants/catalog";
import type { InstructorDetail, InstructorSummary, OpenSlotList, PublicReview } from "@/features/marketplace/types";

export const instructorsApi = {
  browse(city: string, area: string, mode: SessionModeFilter) {
    return http
      .get<InstructorSummary[]>("/api/providers", {
        params: {
          city,
          area,
          category: categorySlug,
          ...(mode ? { mode } : {}),
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

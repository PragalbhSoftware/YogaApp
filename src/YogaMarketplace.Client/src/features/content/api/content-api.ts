import { http } from "@/services/http/http-client";
import type { ContentPage, ContentSlug, UpdateContentPageInput } from "@/features/content/types";

export const contentApi = {
  page(slug: ContentSlug) {
    return http.get<ContentPage>(`/api/pages/${slug}`).then((res) => res.data);
  },

  adminPages() {
    return http.get<ContentPage[]>("/api/admin/pages").then((res) => res.data);
  },

  updatePage({ slug, ...input }: UpdateContentPageInput) {
    return http.put<ContentPage>(`/api/admin/pages/${slug}`, input).then((res) => res.data);
  },
};

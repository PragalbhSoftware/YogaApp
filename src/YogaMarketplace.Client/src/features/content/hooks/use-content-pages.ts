import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { contentApi } from "@/features/content/api/content-api";
import type { ContentSlug, UpdateContentPageInput } from "@/features/content/types";
import { ApiError } from "@/services/http/api-error";

export function useContentPage(slug: ContentSlug) {
  return useQuery({
    queryKey: ["content-page", slug],
    queryFn: () => contentApi.page(slug),
  });
}

export function useAdminContentPages() {
  return useQuery({
    queryKey: ["admin-content-pages"],
    queryFn: contentApi.adminPages,
  });
}

export function useUpdateContentPage() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: UpdateContentPageInput) => contentApi.updatePage(input),
    onSuccess: (page) => {
      void queryClient.invalidateQueries({ queryKey: ["admin-content-pages"] });
      queryClient.setQueryData(["content-page", page.slug], page);
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 409) {
        void queryClient.invalidateQueries({ queryKey: ["admin-content-pages"] });
      }
    },
  });
}

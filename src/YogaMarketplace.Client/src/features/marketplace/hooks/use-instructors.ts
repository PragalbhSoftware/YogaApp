import { useQuery } from "@tanstack/react-query";
import type { SessionMode, SessionModeFilter } from "@/constants/catalog";
import { instructorsApi } from "@/features/marketplace/api/instructors-api";

export function useInstructors(
  cityId: string | null,
  areaId: string | null,
  mode: SessionModeFilter,
  page = 1,
) {
  return useQuery({
    queryKey: ["instructors", cityId, areaId ?? null, mode, page],
    queryFn: () => instructorsApi.browse(cityId ?? "", areaId, mode, page),
    enabled: Boolean(cityId),
    placeholderData: (previousData, previousQuery) => {
      const previous = previousQuery?.queryKey;
      if (!previous) return undefined;
      const [, previousCity, previousArea, previousMode, previousPage] = previous;
      if (
        previousCity === cityId &&
        previousArea === (areaId ?? null) &&
        previousMode === mode &&
        previousPage !== page
      ) {
        return previousData;
      }
      return undefined;
    },
  });
}

export function useInstructor(id: string | undefined) {
  return useQuery({
    queryKey: ["instructor", id],
    queryFn: () => instructorsApi.get(id ?? ""),
    enabled: Boolean(id),
  });
}

export function useInstructorReviews(id: string | undefined) {
  return useQuery({
    queryKey: ["instructor-reviews", id],
    queryFn: () => instructorsApi.reviews(id ?? ""),
    enabled: Boolean(id),
  });
}

export function useInstructorOpenSlots(
  id: string | undefined,
  mode: SessionMode,
  from: string,
  to: string,
  enabled: boolean,
) {
  return useQuery({
    queryKey: ["instructor-open-slots", id, mode, from, to],
    queryFn: () => instructorsApi.slots(id ?? "", mode, from, to),
    enabled: Boolean(id) && enabled,
  });
}

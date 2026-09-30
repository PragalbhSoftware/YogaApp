import { useQuery } from "@tanstack/react-query";
import type { SessionMode, SessionModeFilter } from "@/constants/catalog";
import { instructorsApi } from "@/features/marketplace/api/instructors-api";

export function useInstructors(city: string | null, areaName: string | null, mode: SessionModeFilter) {
  return useQuery({
    queryKey: ["instructors", city, areaName, mode],
    queryFn: () => instructorsApi.browse(city ?? "", areaName ?? "", mode),
    enabled: Boolean(city && areaName),
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

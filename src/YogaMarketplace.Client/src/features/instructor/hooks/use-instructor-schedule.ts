import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { SessionMode } from "@/constants/catalog";
import { invalidateBookingLists } from "@/features/bookings/hooks/use-bookings";
import { instructorApi } from "@/features/instructor/api/instructor-api";
import type {
  AddSlotInput,
  RegisterInstructorInput,
  UpdateInstructorProfileInput,
  UpdateInstructorRatesInput,
  UpdateSlotInput,
} from "@/features/instructor/types";
import { useAuthStore } from "@/stores/auth-store";

export function useInstructorProfile() {
  return useQuery({
    queryKey: ["instructor-me"],
    queryFn: instructorApi.me,
  });
}

export function useUpdateInstructorProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: UpdateInstructorProfileInput) => instructorApi.updateProfile(input),
    onSuccess: (provider) => {
      queryClient.setQueryData(["instructor-me"], provider);
      const current = useAuthStore.getState().user;
      const token = useAuthStore.getState().token;
      if (current && token) {
        useAuthStore.getState().signIn(token, { ...current, name: provider.displayName });
      }
      void queryClient.invalidateQueries({ queryKey: ["instructors"] });
      void queryClient.invalidateQueries({ queryKey: ["instructor", provider.id] });
      void queryClient.invalidateQueries({ queryKey: ["instructor-slots"] });
    },
  });
}

export function useInstructorPayouts(status: string | undefined) {
  const key = status ?? "all";
  return useQuery({
    queryKey: ["instructor-payouts", key],
    queryFn: () => instructorApi.payouts(status),
  });
}

export function useUpdateInstructorRates() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: UpdateInstructorRatesInput) => instructorApi.updateRates(input),
    onSuccess: (provider) => {
      queryClient.setQueryData(["instructor-me"], provider);
      void queryClient.invalidateQueries({ queryKey: ["instructors"] });
      void queryClient.invalidateQueries({ queryKey: ["instructor", provider.id] });
    },
  });
}

export function useRegisterInstructor() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: RegisterInstructorInput) => instructorApi.register(input),
    onSuccess: (result) => {
      const current = useAuthStore.getState().user;
      useAuthStore.getState().signIn(result.token, {
        id: current?.id ?? result.provider.userId,
        name: current?.name || result.provider.displayName,
        phone: current?.phone ?? "",
        gender: current?.gender ?? null,
        role: "Provider",
      });
      queryClient.setQueryData(["instructor-me"], result.provider);
      void queryClient.removeQueries({ queryKey: ["bookings-me"] });
      void queryClient.removeQueries({ queryKey: ["profile"] });
      void queryClient.removeQueries({ queryKey: ["instructors"] });
    },
  });
}

export function useInstructorSlots(mode: SessionMode, from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ["instructor-slots", mode, from, to],
    queryFn: () => instructorApi.slots(mode, from, to),
    enabled,
  });
}

export function useInstructorBookings(enabled: boolean) {
  return useQuery({
    queryKey: ["instructor-bookings", "all"],
    queryFn: () => instructorApi.bookings(),
    enabled,
  });
}

export function useInstructorRequests(status: string | undefined) {
  const key = status ?? "all";
  return useQuery({
    queryKey: ["instructor-bookings", key],
    queryFn: () => instructorApi.bookings(status),
  });
}

export function useAcceptBooking() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => instructorApi.accept(id),
    onSuccess: () => invalidateBookingLists(queryClient),
  });
}

export function useDeclineBooking() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => instructorApi.decline(id),
    onSuccess: () => invalidateBookingLists(queryClient),
  });
}

export function useCompleteBooking() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => instructorApi.complete(id),
    onSuccess: () => {
      invalidateBookingLists(queryClient);
      void queryClient.invalidateQueries({ queryKey: ["instructor-payouts"] });
    },
  });
}

export function useMarkNoShow() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => instructorApi.markNoShow(id),
    onSuccess: () => {
      invalidateBookingLists(queryClient);
      void queryClient.invalidateQueries({ queryKey: ["instructor-payouts"] });
    },
  });
}

export function useAddSlots() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: AddSlotInput) => instructorApi.addSlots(input),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["instructor-slots"] });
    },
  });
}

export function useUpdateSlot() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: UpdateSlotInput) => instructorApi.updateSlot(input),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["instructor-slots"] });
      void queryClient.invalidateQueries({ queryKey: ["instructor-open-slots"] });
    },
  });
}

export function useBlockSlot() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => instructorApi.blockSlot(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["instructor-slots"] });
    },
  });
}

export function useUnblockSlot() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => instructorApi.unblockSlot(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["instructor-slots"] });
    },
  });
}

export function useDeleteSlot() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => instructorApi.deleteSlot(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["instructor-slots"] });
      void queryClient.invalidateQueries({ queryKey: ["instructor-open-slots"] });
    },
  });
}

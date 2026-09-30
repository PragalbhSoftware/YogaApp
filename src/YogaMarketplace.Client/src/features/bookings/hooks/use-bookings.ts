import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { bookingsApi } from "@/features/bookings/api/bookings-api";
import type { ConfirmPaymentInput, CreateOrderInput, CreateReviewInput } from "@/features/bookings/types";

export function invalidateBookingLists(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: ["bookings-me"] });
  void queryClient.invalidateQueries({ queryKey: ["instructor-bookings"] });
  void queryClient.invalidateQueries({ queryKey: ["instructor-slots"] });
  void queryClient.invalidateQueries({ queryKey: ["instructor-open-slots"] });
}

export function useCreateBookingOrder() {
  return useMutation({
    mutationFn: (input: CreateOrderInput) => bookingsApi.createOrder(input),
  });
}

export function useConfirmLocalPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (orderId: string) => bookingsApi.confirmLocal(orderId),
    onSuccess: () => invalidateBookingLists(queryClient),
  });
}

export function useConfirmPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: ConfirmPaymentInput) => bookingsApi.confirm(input.orderId, input.paymentId, input.signature),
    onSuccess: () => invalidateBookingLists(queryClient),
  });
}

export function useMyBookings() {
  return useQuery({
    queryKey: ["bookings-me"],
    queryFn: bookingsApi.listMine,
  });
}

export function useCancelQuote(bookingId: string | null) {
  return useQuery({
    queryKey: ["booking-cancel-quote", bookingId],
    queryFn: () => bookingsApi.cancelQuote(bookingId ?? ""),
    enabled: Boolean(bookingId),
    staleTime: 0,
    gcTime: 0,
  });
}

export function useCancelBooking() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => bookingsApi.cancel(id),
    onSuccess: () => {
      invalidateBookingLists(queryClient);
      void queryClient.invalidateQueries({ queryKey: ["instructor-payouts"] });
    },
  });
}

export function useRescheduleBooking() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { bookingId: string; slotId: string }) =>
      bookingsApi.reschedule(input.bookingId, input.slotId),
    onSuccess: () => invalidateBookingLists(queryClient),
  });
}

export function useCreateReview() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { bookingId: string } & CreateReviewInput) =>
      bookingsApi.review(input.bookingId, { rating: input.rating, comment: input.comment }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["bookings-me"] });
      void queryClient.invalidateQueries({ queryKey: ["instructors"] });
      void queryClient.invalidateQueries({ queryKey: ["instructor"] });
      void queryClient.invalidateQueries({ queryKey: ["instructor-reviews"] });
    },
  });
}

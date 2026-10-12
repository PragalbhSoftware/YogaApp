import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { bookingsApi } from "@/features/bookings/api/bookings-api";
import type {
  BookingQuote,
  ConfirmPaymentInput,
  CreateOrderInput,
  CreateReviewInput,
} from "@/features/bookings/types";
import { myCreditsKey } from "@/features/credits/hooks/use-credits";
import { withRetry } from "@/services/http/retry";

export function invalidateBookingLists(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: ["bookings-me"] });
  void queryClient.invalidateQueries({ queryKey: myCreditsKey });
  void queryClient.invalidateQueries({ queryKey: ["instructor-bookings"] });
  void queryClient.invalidateQueries({ queryKey: ["instructor-slots"] });
  void queryClient.invalidateQueries({ queryKey: ["instructor-open-slots"] });
}

export function useBookingQuote(slotId: string | undefined, trial: boolean, enabled: boolean) {
  return useQuery({
    queryKey: ["booking-quote", slotId, trial],
    queryFn: () => bookingsApi.quote(slotId ?? "", trial),
    enabled: enabled && Boolean(slotId),
    staleTime: 0,
  });
}

export function useTrialStatus(providerId: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: ["trial-status", providerId],
    queryFn: () => bookingsApi.trialStatus(providerId ?? ""),
    enabled: enabled && Boolean(providerId),
    staleTime: 0,
  });
}

export function useCreateBookingOrder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateOrderInput) => bookingsApi.createOrder(input),
    onSuccess: (order, input) => {
      void queryClient.invalidateQueries({ queryKey: ["trial-status"] });
      if (input.useCredit) return;
      queryClient.setQueryData<BookingQuote>(["booking-quote", order.slotId, Boolean(input.trial)], (current) =>
        current
          ? {
              ...current,
              sessionAmount: order.sessionAmount,
              convenienceFee: order.convenienceFee,
              total: order.amount,
              currency: order.currency,
            }
          : current,
      );
    },
  });
}

export function useBookWithCredit() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateOrderInput) => bookingsApi.bookWithCredit(input),
    onSuccess: () => invalidateBookingLists(queryClient),
  });
}

export function useConfirmLocalPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (orderId: string) => bookingsApi.confirmLocal(orderId),
    onSuccess: () => invalidateBookingLists(queryClient),
  });
}

const CONFIRM_RETRY_MS = [1000, 3000, 6000] as const;

/** Confirm is idempotent per payment, so a dropped connection after the customer has paid is retried. */
export function useConfirmPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: ConfirmPaymentInput) =>
      withRetry(() => bookingsApi.confirm(input.orderId, input.paymentId, input.signature), CONFIRM_RETRY_MS),
    onSuccess: () => invalidateBookingLists(queryClient),
    onError: () => invalidateBookingLists(queryClient),
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

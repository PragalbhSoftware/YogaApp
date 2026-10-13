import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { creditsApi } from "@/features/credits/api/credits-api";
import type { ConfirmPaymentInput } from "@/features/bookings/types";

export const myCreditsKey = ["credits-me"] as const;
export const packOrderPollMs = 2000;

export function invalidateCredits(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: myCreditsKey });
  void queryClient.invalidateQueries({ queryKey: ["booking-quote"] });
}

export function usePacks() {
  return useQuery({ queryKey: ["packs"], queryFn: creditsApi.listPacks });
}

export function useMyCredits(enabled = true) {
  return useQuery({ queryKey: myCreditsKey, queryFn: creditsApi.mine, enabled, staleTime: 0 });
}

/**
 * Polls an order the server hasn't captured yet, and stops once it is Completed or Refunded. Failed keeps polling:
 * it only reflects an earlier attempt on the order, and the payment Razorpay just reported can still be captured.
 */
export function usePackOrderStatus(orderId: string | null) {
  return useQuery({
    queryKey: ["pack-order", orderId],
    queryFn: () => creditsApi.orderStatus(orderId ?? ""),
    enabled: Boolean(orderId),
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return !status || status === "Open" || status === "Failed" ? packOrderPollMs : false;
    },
    staleTime: 0,
    gcTime: 0,
  });
}

export function useCreatePackOrder() {
  return useMutation({ mutationFn: (packId: string) => creditsApi.createOrder(packId) });
}

export function useConfirmPackPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: ConfirmPaymentInput) => creditsApi.confirm(input.orderId, input.paymentId, input.signature),
    onSettled: () => invalidateCredits(queryClient),
  });
}

export function useConfirmLocalPackPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (orderId: string) => creditsApi.confirmLocal(orderId),
    onSettled: () => invalidateCredits(queryClient),
  });
}

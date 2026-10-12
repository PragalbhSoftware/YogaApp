import { useEffect, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { digitsOnly } from "@/features/auth/utils/phone";
import {
  openRazorpayCheckout,
  RazorpayCheckoutCancelled,
  RazorpayCheckoutFailed,
} from "@/features/bookings/lib/razorpay-checkout";
import { isFakeOrder } from "@/features/bookings/types";
import {
  invalidateCredits,
  useConfirmLocalPackPayment,
  useConfirmPackPayment,
  useCreatePackOrder,
  usePackOrderStatus,
} from "@/features/credits/hooks/use-credits";
import type { Pack, PackOrderStatus } from "@/features/credits/types";
import { purchaseStage } from "@/features/credits/utils/purchase-stage";
import { isRetryable, toUserMessage } from "@/services/http/api-error";
import { useAuthStore } from "@/stores/auth-store";

/**
 * Buys a pack: opens a server order, takes payment in Razorpay, then waits for the server to capture it. A confirm
 * that fails on the network keeps polling, because the Razorpay webhook may still capture the payment.
 */
export function useBuyPack() {
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const createOrder = useCreatePackOrder();
  const confirm = useConfirmPackPayment();
  const confirmLocal = useConfirmLocalPackPayment();
  const [buyingId, setBuyingId] = useState<string | null>(null);
  const [orderId, setOrderId] = useState<string | null>(null);
  const [confirmed, setConfirmed] = useState<PackOrderStatus | null>(null);
  const known = confirmed?.orderId === orderId ? confirmed : null;
  const poll = usePackOrderStatus(orderId && known?.status !== "Completed" && known?.status !== "Refunded" ? orderId : null);
  const status = known && known.status !== "Open" ? known : (poll.data ?? known);
  const stage = purchaseStage(orderId, status);

  useEffect(() => {
    if (stage === "done") invalidateCredits(queryClient);
  }, [stage, queryClient]);

  async function buy(pack: Pack) {
    setBuyingId(pack.id);
    try {
      const order = await createOrder.mutateAsync(pack.id);
      if (order.localCapture || isFakeOrder(order.orderId)) {
        const result = await confirmLocal.mutateAsync(order.orderId);
        setOrderId(order.orderId);
        setConfirmed(result);
        return;
      }

      const paid = await openRazorpayCheckout({
        keyId: order.keyId,
        orderId: order.orderId,
        amountPaise: order.amountPaise,
        currency: order.currency,
        description: order.packName,
        customerName: user?.name,
        customerPhone: digitsOnly(user?.phone ?? ""),
      });
      setOrderId(paid.orderId);
      try {
        setConfirmed(await confirm.mutateAsync(paid));
      } catch (error) {
        if (!isRetryable(error)) {
          reset();
          throw error;
        }
      }
    } catch (error) {
      if (error instanceof RazorpayCheckoutCancelled) {
        toast.message("Payment cancelled. No pack was bought.");
        return;
      }
      if (error instanceof RazorpayCheckoutFailed) {
        toast.error(error.message);
        return;
      }
      toast.error(toUserMessage(error));
    } finally {
      setBuyingId(null);
    }
  }

  function reset() {
    setOrderId(null);
    setConfirmed(null);
  }

  return { buy, reset, buyingId, stage, batch: status?.batch ?? null, busy: buyingId !== null || stage === "confirming" };
}

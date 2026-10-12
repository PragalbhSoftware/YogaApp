import { toast } from "sonner";
import { digitsOnly } from "@/features/auth/utils/phone";
import {
  useBookWithCredit,
  useConfirmLocalPayment,
  useConfirmPayment,
  useCreateBookingOrder,
} from "@/features/bookings/hooks/use-bookings";
import {
  openRazorpayCheckout,
  RazorpayCheckoutCancelled,
  RazorpayCheckoutFailed,
} from "@/features/bookings/lib/razorpay-checkout";
import { isFakeOrder, type BookingQuote, type BookingRecord } from "@/features/bookings/types";
import type { HomeVisitValues } from "@/features/bookings/schemas";
import { isLostCredit, type PayMethod } from "@/features/bookings/utils/pay-method";
import { isRetryable, toUserMessage } from "@/services/http/api-error";
import { useAuthStore } from "@/stores/auth-store";

const PAID_NOT_CONFIRMED =
  "Your payment went through. We're still confirming the booking, so check My Bookings in a few minutes and don't pay again.";

type BookCheckoutOptions = {
  slotId: string | undefined;
  trial: boolean;
  method: PayMethod;
  quote: BookingQuote | undefined;
  onBooked: (booking: BookingRecord) => void;
  /** The credit went to another booking or stopped covering the slot: refetch the quote and switch to paying. */
  onLostCredit: () => void;
  onCheckoutOpenChange: (open: boolean) => void;
};

/**
 * Books the slot the way the customer chose: with a credit and nothing to pay, with a credit plus the convenience
 * fee through Razorpay, or by paying the full price through Razorpay.
 */
export function useBookCheckout({
  slotId,
  trial,
  method,
  quote,
  onBooked,
  onLostCredit,
  onCheckoutOpenChange,
}: BookCheckoutOptions) {
  const user = useAuthStore((state) => state.user);
  const createOrder = useCreateBookingOrder();
  const bookWithCredit = useBookWithCredit();
  const confirmLocal = useConfirmLocalPayment();
  const confirmPayment = useConfirmPayment();
  const pending =
    createOrder.isPending || bookWithCredit.isPending || confirmLocal.isPending || confirmPayment.isPending;

  function booked(booking: BookingRecord) {
    onBooked(booking);
    toast.success("Booked. The instructor will accept your request.");
  }

  async function checkout(home?: HomeVisitValues) {
    if (!slotId) return;
    const useCredit = method === "credit";
    const input = { slotId, trial, useCredit, homeAddress: home?.homeAddress, landmark: home?.landmark };
    try {
      if (useCredit && (quote?.credit.total ?? 0) === 0) {
        booked(await bookWithCredit.mutateAsync(input));
        return;
      }

      const order = await createOrder.mutateAsync(input);
      if (order.localCapture || isFakeOrder(order.orderId)) {
        booked(await confirmLocal.mutateAsync(order.orderId));
        return;
      }

      onCheckoutOpenChange(true);
      const paid = await openRazorpayCheckout({
        keyId: order.keyId,
        orderId: order.orderId,
        amountPaise: order.amountPaise,
        currency: order.currency,
        description: useCredit
          ? `Convenience fee for a credit session with ${order.providerName}`
          : `${trial ? "Trial" : order.mode} session with ${order.providerName}`,
        customerName: user?.name,
        customerPhone: digitsOnly(user?.phone ?? ""),
      });
      try {
        booked(await confirmPayment.mutateAsync(paid));
      } catch (error) {
        if (!isRetryable(error)) throw error;
        toast.warning(PAID_NOT_CONFIRMED, { duration: 12000 });
      }
    } catch (error) {
      if (error instanceof RazorpayCheckoutCancelled) {
        toast.message(error.message);
        return;
      }
      if (error instanceof RazorpayCheckoutFailed) {
        toast.error(error.message);
        return;
      }
      if (useCredit && isLostCredit(error)) onLostCredit();
      toast.error(toUserMessage(error));
    } finally {
      onCheckoutOpenChange(false);
    }
  }

  return { checkout, pending };
}

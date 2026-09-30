import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { routes } from "@/constants/routes";
import { CancelDialog } from "@/features/bookings/components/cancel-dialog";
import { CustomerBookingCard } from "@/features/bookings/components/customer-booking-card";
import { RescheduleDialog } from "@/features/bookings/components/reschedule-dialog";
import {
  useCancelBooking,
  useCreateReview,
  useMyBookings,
  useRescheduleBooking,
} from "@/features/bookings/hooks/use-bookings";
import type { BookingRecord, CreateReviewInput } from "@/features/bookings/types";
import { toUserMessage } from "@/services/http/api-error";
import { formatInr } from "@/utils/money";

export function BookingsPage() {
  const navigate = useNavigate();
  const bookings = useMyBookings();
  const cancel = useCancelBooking();
  const reschedule = useRescheduleBooking();
  const review = useCreateReview();
  const [moving, setMoving] = useState<BookingRecord | null>(null);
  const [cancelling, setCancelling] = useState<BookingRecord | null>(null);

  async function onCancel(id: string) {
    try {
      const result = await cancel.mutateAsync(id);
      setCancelling(null);
      toast.success(
        result.lateCancelFee
          ? `Cancelled. ${formatInr(result.refundedAmount)} is on its way back to you.`
          : "Cancelled. Your full refund is on its way.",
      );
    } catch (error) {
      toast.error(toUserMessage(error));
    }
  }

  async function onReschedule(slotId: string) {
    if (!moving) return;
    try {
      await reschedule.mutateAsync({ bookingId: moving.id, slotId });
      setMoving(null);
      toast.success("Rescheduled. The session moved. Payment stays paid.");
    } catch (error) {
      toast.error(toUserMessage(error));
    }
  }

  async function onReview(bookingId: string, values: CreateReviewInput) {
    try {
      await review.mutateAsync({ bookingId, ...values });
      toast.success("Thanks. Your review is saved.");
    } catch (error) {
      toast.error(toUserMessage(error));
    }
  }

  const busyId = cancel.isPending
    ? cancel.variables
    : reschedule.isPending
      ? reschedule.variables?.bookingId
      : review.isPending
        ? review.variables?.bookingId
        : undefined;

  return (
    <main className="mx-auto max-w-3xl space-y-6 px-4 py-8 sm:px-8">
      <header className="space-y-1">
        <p className="text-[11px] font-medium tracking-[0.22em] text-brand-muted uppercase">Bookings</p>
        <h1 className="font-heading text-2xl font-medium">My bookings</h1>
        <p className="text-sm leading-relaxed text-brand-muted">
          Sessions you have paid for. After the instructor accepts, you can move to another open time. After a session
          is complete, you can leave one rating. Times are India Standard Time.
        </p>
      </header>

      {bookings.isLoading ? <BookingListSkeleton /> : null}
      {bookings.isError ? (
        <ErrorState message={toUserMessage(bookings.error)} onRetry={() => void bookings.refetch()} />
      ) : null}

      {bookings.isSuccess && bookings.data.length === 0 ? (
        <EmptyState
          title="You have no bookings yet"
          description="Browse instructors and book an open time."
          actionLabel="Find instructors"
          onAction={() => navigate(routes.home)}
        />
      ) : null}

      {bookings.isSuccess && bookings.data.length > 0 ? (
        <div className="space-y-4">
          {bookings.data.map((booking) => (
            <CustomerBookingCard
              key={booking.id}
              booking={booking}
              busy={busyId === booking.id}
              onCancel={setCancelling}
              onReschedule={setMoving}
              onReview={(id, values) => void onReview(id, values)}
            />
          ))}
        </div>
      ) : null}

      <CancelDialog
        booking={cancelling}
        busy={cancel.isPending}
        onClose={() => setCancelling(null)}
        onConfirm={(id) => void onCancel(id)}
      />

      <RescheduleDialog
        booking={moving}
        busy={reschedule.isPending}
        onClose={() => setMoving(null)}
        onConfirm={(slotId) => void onReschedule(slotId)}
      />
    </main>
  );
}

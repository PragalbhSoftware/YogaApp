import { Button, Chip } from "@mui/material";
import { ReviewForm } from "@/features/bookings/components/review-form";
import { SessionFacts } from "@/features/bookings/components/session-facts";
import type { BookingRecord, CreateReviewInput } from "@/features/bookings/types";
import {
  canCancel,
  canReschedule,
  canSubmitReview,
  showReviewedNote,
  statusFilterLabel,
} from "@/features/bookings/utils/status";
import { initials } from "@/utils/initials";

type CustomerBookingCardProps = {
  booking: BookingRecord;
  busy: boolean;
  onCancel: (booking: BookingRecord) => void;
  onReschedule: (booking: BookingRecord) => void;
  onReview: (bookingId: string, values: CreateReviewInput) => void;
};

export function CustomerBookingCard({
  booking,
  busy,
  onCancel,
  onReschedule,
  onReview,
}: CustomerBookingCardProps) {
  const showCancel = canCancel(booking);
  const showReschedule = canReschedule(booking);
  const showReview = canSubmitReview(booking);
  const reviewed = showReviewedNote(booking);

  return (
    <article className="rounded-3xl border border-brand-border bg-brand-surface p-5 shadow-[0_8px_24px_rgba(37,49,39,0.05)]">
      <div className="flex items-start gap-3">
        <span
          className="flex size-12 shrink-0 items-center justify-center rounded-full bg-brand-background font-heading text-sm font-medium text-brand-primary"
          aria-hidden="true"
        >
          {initials(booking.providerName)}
        </span>
        <div className="min-w-0 flex-1">
          <h2 className="font-heading text-lg font-medium">{booking.providerName}</h2>
          <p className="text-sm text-brand-muted">{booking.serviceTitle}</p>
          <Chip size="small" label={statusFilterLabel(booking.status)} sx={{ mt: 1 }} />
        </div>
      </div>
      <div className="mt-4">
        <SessionFacts booking={booking} />
      </div>
      {showReview ? (
        <div className="mt-4">
          <ReviewForm busy={busy} onSubmit={(values) => onReview(booking.id, values)} />
        </div>
      ) : null}
      {reviewed ? (
        <p className="mt-4 text-sm text-brand-muted" role="status">
          You reviewed this session.
        </p>
      ) : null}
      {showCancel || showReschedule ? (
        <div className="mt-4 flex flex-col gap-2">
          {showReschedule ? (
            <>
              <p className="text-sm text-brand-muted">
                Move to another open {booking.mode.toLowerCase()} time with this instructor. Payment stays paid.
              </p>
              <Button
                variant="contained"
                fullWidth
                disabled={busy}
                onClick={() => onReschedule(booking)}
                sx={{ minHeight: 44, borderRadius: "14px" }}
              >
                Reschedule
              </Button>
            </>
          ) : null}
          {showCancel ? (
            <>
              <p className="text-sm text-brand-muted">
                {booking.status === "Upcoming"
                  ? "Free until the cancellation window closes. You’ll see the refund before you confirm."
                  : "Free while the instructor hasn’t accepted. The slot opens up again."}
              </p>
              <Button
                variant="outlined"
                fullWidth
                disabled={busy}
                onClick={() => onCancel(booking)}
                sx={{ minHeight: 44, borderRadius: "14px" }}
              >
                {busy ? "Cancelling…" : "Cancel booking"}
              </Button>
            </>
          ) : null}
        </div>
      ) : null}
    </article>
  );
}

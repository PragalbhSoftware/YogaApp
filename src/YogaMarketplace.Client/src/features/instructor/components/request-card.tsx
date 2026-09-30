import { useState } from "react";
import { Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle } from "@mui/material";
import { SessionFacts } from "@/features/bookings/components/session-facts";
import type { BookingRecord } from "@/features/bookings/types";
import { canAccept, canComplete, canMarkNoShow, statusFilterLabel } from "@/features/bookings/utils/status";

type RequestCardProps = {
  booking: BookingRecord;
  busy: boolean;
  onAccept: (id: string) => void;
  onDecline: (id: string) => void;
  onComplete: (id: string) => void;
  onNoShow: (id: string) => void;
};

export function RequestCard({
  booking,
  busy,
  onAccept,
  onDecline,
  onComplete,
  onNoShow,
}: RequestCardProps) {
  const [confirmNoShow, setConfirmNoShow] = useState(false);
  const upcoming = canComplete(booking.status) && canMarkNoShow(booking.status);

  return (
    <article className="rounded-3xl border border-brand-border bg-brand-surface p-5 shadow-[0_8px_24px_rgba(37,49,39,0.05)]">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 className="font-heading text-lg font-medium">{booking.serviceTitle}</h2>
          <p className="text-sm text-brand-muted">{booking.mode} session</p>
        </div>
        <Chip size="small" label={statusFilterLabel(booking.status)} />
      </div>
      <div className="mt-4">
        <SessionFacts booking={booking} />
      </div>
      {canAccept(booking.status) ? (
        <div className="mt-4 space-y-2">
          <p className="text-sm text-brand-muted">
            Accept keeps the session. Decline refunds the customer and frees the slot.
          </p>
          <div className="grid grid-cols-2 gap-2">
            <Button
              variant="contained"
              disabled={busy}
              onClick={() => onAccept(booking.id)}
              sx={{ minHeight: 44, borderRadius: "14px" }}
            >
              {busy ? "Updating…" : "Accept"}
            </Button>
            <Button
              variant="outlined"
              disabled={busy}
              onClick={() => onDecline(booking.id)}
              sx={{ minHeight: 44, borderRadius: "14px" }}
            >
              Decline
            </Button>
          </div>
        </div>
      ) : null}
      {upcoming ? (
        <div className="mt-4 space-y-2">
          <p className="text-sm text-brand-muted">
            Mark complete so the student can leave a review. If they missed the session, mark no-show.
            Both create your payout.
          </p>
          <div className="grid gap-2 sm:grid-cols-2">
            <Button
              variant="contained"
              disabled={busy}
              onClick={() => onComplete(booking.id)}
              sx={{ minHeight: 44, borderRadius: "14px" }}
            >
              {busy ? "Updating…" : "Mark complete"}
            </Button>
            <Button
              variant="outlined"
              disabled={busy}
              onClick={() => setConfirmNoShow(true)}
              sx={{ minHeight: 44, borderRadius: "14px" }}
            >
              Student didn’t show
            </Button>
          </div>
        </div>
      ) : null}

      <Dialog
        open={confirmNoShow}
        onClose={() => {
          if (!busy) setConfirmNoShow(false);
        }}
        fullWidth
        maxWidth="sm"
      >
        <DialogTitle>Mark as no-show?</DialogTitle>
        <DialogContent>
          The student cannot leave a review. You still receive your share after the platform fee. The
          payment is not refunded.
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button disabled={busy} onClick={() => setConfirmNoShow(false)}>
            Cancel
          </Button>
          <Button
            variant="contained"
            disabled={busy}
            onClick={() => {
              setConfirmNoShow(false);
              onNoShow(booking.id);
            }}
          >
            Mark no-show
          </Button>
        </DialogActions>
      </Dialog>
    </article>
  );
}

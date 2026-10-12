import { Button, Dialog, DialogContent, Skeleton } from "@mui/material";
import { ErrorState } from "@/components/common/error-state";
import { useCancelQuote } from "@/features/bookings/hooks/use-bookings";
import type { BookingRecord, CancelQuote } from "@/features/bookings/types";
import { cancelButtonLabel, quoteNote } from "@/features/bookings/utils/cancel-copy";
import { toUserMessage } from "@/services/http/api-error";
import { formatInr } from "@/utils/money";

type CancelDialogProps = {
  booking: BookingRecord | null;
  busy: boolean;
  onClose: () => void;
  onConfirm: (bookingId: string) => void;
};

export function CancelDialog({ booking, busy, onClose, onConfirm }: CancelDialogProps) {
  const quote = useCancelQuote(booking?.id ?? null);

  return (
    <Dialog
      open={Boolean(booking)}
      onClose={() => {
        if (!busy) onClose();
      }}
      fullWidth
      maxWidth="sm"
      slotProps={{ paper: { sx: { borderRadius: "24px" } } }}
    >
      <DialogContent sx={{ p: 3 }}>
        <div className="flex flex-col gap-5">
          <div>
            <h2 className="font-heading text-lg font-medium">Cancel this booking?</h2>
            <p className="mt-1 text-sm leading-relaxed text-brand-muted">
              {booking?.providerName} · {booking?.mode} session. The slot opens up for other students.
            </p>
          </div>

          {quote.isLoading ? <Skeleton variant="rounded" height={120} sx={{ borderRadius: "16px" }} /> : null}
          {quote.isError ? (
            <ErrorState message={toUserMessage(quote.error)} onRetry={() => void quote.refetch()} />
          ) : null}
          {quote.isSuccess ? <QuoteSummary quote={quote.data} /> : null}

          <div className="flex flex-col gap-2">
            <Button
              variant="contained"
              color={
                quote.data && (quote.data.lateCancelFee > 0 || (quote.data.paidWithCredit && !quote.data.creditReturned))
                  ? "warning"
                  : "primary"
              }
              disabled={busy || !quote.isSuccess || !booking}
              onClick={() => booking && onConfirm(booking.id)}
              sx={{ minHeight: 48, borderRadius: "14px" }}
            >
              {busy ? "Cancelling…" : quote.data ? cancelButtonLabel(quote.data) : "Cancel booking"}
            </Button>
            <Button variant="text" disabled={busy} onClick={onClose} sx={{ minHeight: 44 }}>
              Keep my booking
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function QuoteSummary({ quote }: { quote: CancelQuote }) {
  if (quote.paidWithCredit) return <CreditQuoteSummary quote={quote} />;
  const late = quote.lateCancelFee > 0;

  return (
    <div className="flex flex-col gap-3 rounded-2xl border border-brand-border bg-brand-background p-4">
      <dl className="flex flex-col gap-2 text-sm">
        <div className="flex justify-between gap-3">
          <dt className="text-brand-muted">You paid</dt>
          <dd className="tabular-nums">{formatInr(quote.amount)}</dd>
        </div>
        {late ? (
          <div className="flex justify-between gap-3">
            <dt className="text-brand-muted">
              Late-cancel fee{quote.lateCancelFeeType === "Percent" ? ` (${quote.lateCancelFeeValue}%)` : ""}
            </dt>
            <dd className="tabular-nums">− {formatInr(quote.lateCancelFee)}</dd>
          </div>
        ) : null}
        {quote.convenienceFeeKept > 0 ? (
          <div className="flex justify-between gap-3">
            <dt className="text-brand-muted">Convenience fee (not refunded)</dt>
            <dd className="tabular-nums">− {formatInr(quote.convenienceFeeKept)}</dd>
          </div>
        ) : null}
        <div className="flex justify-between gap-3 border-t border-brand-border pt-2 font-medium">
          <dt>Refund</dt>
          <dd className="tabular-nums">{formatInr(quote.refund)}</dd>
        </div>
      </dl>
      <p className="text-sm leading-relaxed text-brand-muted">{quoteNote(quote)}</p>
    </div>
  );
}

function CreditQuoteSummary({ quote }: { quote: CancelQuote }) {
  return (
    <div className="flex flex-col gap-3 rounded-2xl border border-brand-border bg-brand-background p-4">
      <dl className="flex flex-col gap-2 text-sm">
        <div className="flex justify-between gap-3">
          <dt className="text-brand-muted">You paid</dt>
          <dd>{quote.amount > 0 ? `1 credit + ${formatInr(quote.amount)}` : "1 credit"}</dd>
        </div>
        <div className="flex justify-between gap-3 border-t border-brand-border pt-2 font-medium">
          <dt>Credit</dt>
          <dd>{quote.creditReturned ? "Returned" : "Used"}</dd>
        </div>
        {quote.refund > 0 ? (
          <div className="flex justify-between gap-3 font-medium">
            <dt>Refund</dt>
            <dd className="tabular-nums">{formatInr(quote.refund)}</dd>
          </div>
        ) : null}
      </dl>
      <p className="text-sm leading-relaxed text-brand-muted">{quoteNote(quote)}</p>
    </div>
  );
}
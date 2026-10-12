import { Skeleton } from "@mui/material";
import { PriceText } from "@/components/common/price-text";
import type { BookingQuote } from "@/features/bookings/types";
import type { InstructorDetail, OpenSlot } from "@/features/marketplace/types";
import { formatSlotDay, formatTimeRange12 } from "@/utils/clock";

type BookSummaryProps = {
  instructor: InstructorDetail;
  slot: OpenSlot;
  quote: BookingQuote | undefined;
  quoteLoading: boolean;
  withCredit: boolean;
};

export function BookSummary({ instructor, slot, quote, quoteLoading, withCredit }: BookSummaryProps) {
  return (
    <article className="rounded-3xl border border-brand-border bg-brand-surface p-5">
      <p className="font-heading text-lg font-medium">{instructor.displayName}</p>
      <p className="text-sm text-brand-muted">{instructor.area}</p>
      <dl className="mt-4 space-y-2 text-sm">
        <div className="flex justify-between gap-3">
          <dt className="text-brand-muted">Session</dt>
          <dd>{slot.mode}</dd>
        </div>
        <div className="flex justify-between gap-3">
          <dt className="text-brand-muted">When</dt>
          <dd className="text-right">
            {formatSlotDay(slot.date)}
            <span className="mt-0.5 block">
              {formatTimeRange12(slot.start, slot.end)}
            </span>
          </dd>
        </div>
        {quoteLoading ? <Skeleton variant="rounded" height={64} aria-label="Loading price" /> : null}
        {quote && withCredit ? <CreditLines quote={quote} /> : null}
        {quote && !withCredit ? <QuoteLines quote={quote} /> : null}
      </dl>
    </article>
  );
}

function CreditLines({ quote }: { quote: BookingQuote }) {
  return (
    <>
      <div className="flex justify-between gap-3">
        <dt className="text-brand-muted">Fee</dt>
        <dd>1 credit</dd>
      </div>
      {quote.credit.convenienceFee > 0 ? (
        <div className="flex justify-between gap-3">
          <dt className="text-brand-muted">Convenience fee</dt>
          <dd>
            <PriceText amount={quote.credit.convenienceFee} />
          </dd>
        </div>
      ) : null}
      <div className="flex justify-between gap-3 border-t border-brand-border pt-2 font-medium">
        <dt>To pay</dt>
        <dd>{quote.credit.total === 0 ? "Nothing" : <PriceText amount={quote.credit.total} />}</dd>
      </div>
    </>
  );
}

function QuoteLines({ quote }: { quote: BookingQuote }) {
  return (
    <>
      <div className="flex justify-between gap-3">
        <dt className="text-brand-muted">{quote.isTrial ? "Trial class fee" : "Fee"}</dt>
        <dd>{quote.sessionAmount === 0 ? "Free" : <PriceText amount={quote.sessionAmount} />}</dd>
      </div>
      {quote.convenienceFee > 0 ? (
        <div className="flex justify-between gap-3">
          <dt className="text-brand-muted">Convenience fee</dt>
          <dd>
            <PriceText amount={quote.convenienceFee} />
          </dd>
        </div>
      ) : null}
      <div className="flex justify-between gap-3 border-t border-brand-border pt-2 font-medium">
        <dt>Total</dt>
        <dd>
          <PriceText amount={quote.total} />
        </dd>
      </div>
    </>
  );
}

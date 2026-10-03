import { Skeleton } from "@mui/material";
import { PriceText } from "@/components/common/price-text";
import type { InstructorDetail, OpenSlot } from "@/features/marketplace/types";
import { formatSlotDay, formatTimeRange12 } from "@/utils/clock";

type BookSummaryProps = {
  instructor: InstructorDetail;
  slot: OpenSlot;
  rate: number;
  convenienceFee: number | undefined;
  feeLoading: boolean;
};

export function BookSummary({ instructor, slot, rate, convenienceFee, feeLoading }: BookSummaryProps) {
  const hasFee = (convenienceFee ?? 0) > 0;

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
        <div className="flex justify-between gap-3">
          <dt className="text-brand-muted">Fee</dt>
          <dd>
            <PriceText amount={rate} />
          </dd>
        </div>
        {feeLoading ? <Skeleton variant="text" aria-label="Loading fees" /> : null}
        {!feeLoading && hasFee ? (
          <>
            <div className="flex justify-between gap-3">
              <dt className="text-brand-muted">Convenience fee</dt>
              <dd>
                <PriceText amount={convenienceFee ?? 0} />
              </dd>
            </div>
            <div className="flex justify-between gap-3 border-t border-brand-border pt-2 font-medium">
              <dt>Total</dt>
              <dd>
                <PriceText amount={rate + (convenienceFee ?? 0)} />
              </dd>
            </div>
          </>
        ) : null}
      </dl>
    </article>
  );
}

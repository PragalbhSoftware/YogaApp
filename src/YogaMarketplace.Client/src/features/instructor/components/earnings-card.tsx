import { Chip } from "@mui/material";
import { PriceText } from "@/components/common/price-text";
import { payoutReasonLabel } from "@/features/bookings/utils/status";
import type { InstructorPayout } from "@/features/instructor/types";
import { formatWhenKolkata } from "@/utils/clock";

const statusLabel: Record<string, string> = {
  Pending: "Waiting",
  Exported: "Queued",
  Paid: "Paid",
};

type EarningsCardProps = {
  payout: InstructorPayout;
};

export function EarningsCard({ payout }: EarningsCardProps) {
  const lateCancel = payout.bookingStatus === "Cancelled";

  return (
    <article className="rounded-3xl border border-brand-border bg-brand-surface p-5 shadow-[0_8px_24px_rgba(37,49,39,0.05)]">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="font-medium">{payoutReasonLabel(payout.bookingStatus)}</p>
          <p className="text-sm text-brand-muted">{formatWhenKolkata(payout.createdAt)}</p>
        </div>
        <Chip size="small" label={statusLabel[payout.status] ?? payout.status} />
      </div>
      <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-3">
        <div>
          <dt className="text-brand-muted">{lateCancel ? "Late-cancel fee" : "Student paid"}</dt>
          <dd>
            <PriceText amount={payout.grossAmount} className="min-w-0 text-sm" />
          </dd>
        </div>
        <div>
          <dt className="text-brand-muted">Platform fee ({payout.feePercent}%)</dt>
          <dd>
            <PriceText amount={payout.feeAmount} className="min-w-0 text-sm" />
          </dd>
        </div>
        <div>
          <dt className="text-brand-muted">You receive</dt>
          <dd>
            <PriceText amount={payout.netAmount} className="min-w-0 text-sm" />
          </dd>
        </div>
      </dl>
    </article>
  );
}

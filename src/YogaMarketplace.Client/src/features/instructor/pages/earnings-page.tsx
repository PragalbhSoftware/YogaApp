import { useSearchParams } from "react-router-dom";
import { Chip } from "@mui/material";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { PriceText } from "@/components/common/price-text";
import { EarningsCard } from "@/features/instructor/components/earnings-card";
import { useInstructorPayouts } from "@/features/instructor/hooks/use-instructor-schedule";
import { toUserMessage } from "@/services/http/api-error";

const filters = [
  { value: null, label: "All" },
  { value: "Pending", label: "Waiting" },
  { value: "Paid", label: "Paid" },
] as const;

export function InstructorEarningsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const status = readPayoutStatus(searchParams.get("status"));
  const payouts = useInstructorPayouts(status);
  const waiting = (payouts.data ?? []).filter((row) => row.status === "Pending" || row.status === "Exported");
  const waitingNet = waiting.reduce((sum, row) => sum + row.netAmount, 0);

  function setStatus(next: string | null) {
    const params = new URLSearchParams(searchParams);
    if (next) params.set("status", next);
    else params.delete("status");
    setSearchParams(params, { replace: true });
  }

  return (
    <main className="mx-auto max-w-3xl space-y-6 px-4 py-8 sm:px-8">
      <header className="space-y-1">
        <p className="text-[11px] font-medium tracking-[0.22em] text-brand-muted uppercase">Earnings</p>
        <h1 className="font-heading text-2xl font-medium">Your payouts</h1>
        <p className="text-sm leading-relaxed text-brand-muted">
          After you mark a session complete or no-show, your share appears here. The owner records
          when it is paid.
        </p>
      </header>

      {payouts.isSuccess && payouts.data.length > 0 && !status ? (
        <div className="rounded-3xl border border-brand-border bg-brand-surface p-5">
          <p className="text-sm text-brand-muted">Waiting to be paid</p>
          <p className="mt-1 font-heading text-2xl">
            <PriceText amount={waitingNet} className="min-w-0 text-2xl" />
          </p>
        </div>
      ) : null}

      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Payout status">
        {filters.map((filter) => {
          const selected = (status ?? null) === filter.value;
          return (
            <Chip
              key={filter.label}
              label={filter.label}
              clickable
              color={selected ? "primary" : "default"}
              variant={selected ? "filled" : "outlined"}
              onClick={() => setStatus(filter.value)}
            />
          );
        })}
      </div>

      {payouts.isLoading ? <BookingListSkeleton /> : null}
      {payouts.isError ? (
        <ErrorState message={toUserMessage(payouts.error)} onRetry={() => void payouts.refetch()} />
      ) : null}
      {payouts.isSuccess && payouts.data.length === 0 ? (
        <EmptyState
          title="No payouts in this list"
          description="Mark an upcoming session complete or no-show. Your share after the platform fee shows up here."
        />
      ) : null}
      {payouts.isSuccess && payouts.data.length > 0 ? (
        <div className="space-y-4">
          {payouts.data.map((payout) => (
            <EarningsCard key={payout.id} payout={payout} />
          ))}
        </div>
      ) : null}
    </main>
  );
}

function readPayoutStatus(value: string | null) {
  if (value === "Pending" || value === "Paid" || value === "Exported") return value;
  return undefined;
}

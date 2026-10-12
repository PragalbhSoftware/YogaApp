import { Link } from "react-router-dom";
import { Button, Skeleton } from "@mui/material";
import { Ticket } from "lucide-react";
import { routes } from "@/constants/routes";
import { useMyCredits } from "@/features/credits/hooks/use-credits";
import type { CreditBatch } from "@/features/credits/types";
import { toUserMessage } from "@/services/http/api-error";
import { formatSlotDay } from "@/utils/clock";
import { formatInr } from "@/utils/money";

type CreditsPanelProps = {
  /** With no credits, show a short link to the packs page instead of nothing. */
  showWhenEmpty?: boolean;
};

export function CreditsPanel({ showWhenEmpty = false }: CreditsPanelProps) {
  const credits = useMyCredits();

  if (credits.isLoading) {
    return <Skeleton variant="rounded" height={96} sx={{ borderRadius: "24px" }} aria-label="Loading credits" />;
  }
  if (credits.isError) {
    return (
      <section className="rounded-3xl border border-brand-border bg-brand-surface p-5 text-sm" role="alert">
        <p className="text-brand-muted">Couldn’t load your credits. {toUserMessage(credits.error)}</p>
        <Button size="small" sx={{ mt: 1, px: 0 }} onClick={() => void credits.refetch()}>
          Try again
        </Button>
      </section>
    );
  }
  if (!credits.data || credits.data.total === 0) {
    if (!showWhenEmpty) return null;
    return (
      <section className="flex items-center justify-between gap-3 rounded-3xl border border-brand-border bg-brand-surface p-5">
        <p className="text-sm text-brand-muted">Book often? Save with a session pack.</p>
        <Button component={Link} to={routes.packs} variant="outlined" size="small">
          See packs
        </Button>
      </section>
    );
  }

  return (
    <section
      aria-labelledby="credits-heading"
      className="rounded-3xl border border-brand-border bg-brand-surface p-5 shadow-[0_8px_24px_rgba(37,49,39,0.05)]"
    >
      <div className="flex items-center gap-3">
        <span className="flex size-10 items-center justify-center rounded-full bg-brand-background text-brand-primary">
          <Ticket className="size-5" aria-hidden="true" />
        </span>
        <div>
          <h2 id="credits-heading" className="font-heading text-lg font-medium">
            {credits.data.total} {credits.data.total === 1 ? "credit" : "credits"} left
          </h2>
          <p className="text-sm text-brand-muted">1 credit books one session. Earliest expiry is used first.</p>
        </div>
      </div>
      <ul className="mt-4 space-y-2">
        {credits.data.batches.map((batch) => (
          <BatchRow key={batch.id} batch={batch} />
        ))}
      </ul>
    </section>
  );
}

function BatchRow({ batch }: { batch: CreditBatch }) {
  return (
    <li className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1 rounded-2xl bg-brand-background px-4 py-3 text-sm">
      <span className="font-medium">
        {batch.remaining} of {batch.sessions} · {batch.packName}
      </span>
      <span className="text-brand-muted">
        Use by {formatSlotDay(batch.useBy)} · sessions up to {formatInr(batch.priceCap)}
      </span>
    </li>
  );
}

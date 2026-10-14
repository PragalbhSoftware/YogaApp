import { Button } from "@mui/material";
import type { AdminErrorEvent } from "@/features/admin/types";
import { formatAdminWhen } from "@/features/admin/utils";

type ErrorEventCardProps = {
  error: AdminErrorEvent;
  acknowledging: boolean;
  onAcknowledge: () => void;
};

export function ErrorEventCard({ error, acknowledging, onAcknowledge }: ErrorEventCardProps) {
  return (
    <article className="rounded-[24px] border border-brand-border bg-brand-surface p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <h3 className="min-w-0 font-heading text-lg font-medium break-words">{error.errorType}</h3>
        <p className="text-[11px] font-semibold tracking-[0.14em] text-brand-primary uppercase">
          {error.level} · {error.count}×
        </p>
      </div>
      <dl className="mt-2 grid gap-x-6 gap-y-1 text-sm text-brand-muted sm:grid-cols-2">
        <div>
          <dt className="inline">First seen: </dt>
          <dd className="inline">{formatAdminWhen(error.firstSeenAt)}</dd>
        </div>
        <div>
          <dt className="inline">Last seen: </dt>
          <dd className="inline">{formatAdminWhen(error.lastSeenAt)}</dd>
        </div>
        {error.lastCorrelationId ? (
          <div className="sm:col-span-2">
            <dt className="inline">Correlation id: </dt>
            <dd className="inline font-mono break-all">{error.lastCorrelationId}</dd>
          </div>
        ) : null}
      </dl>
      <p className="mt-3 text-sm break-words">{error.lastMessage}</p>
      {error.acknowledgedAt ? (
        <p className="mt-2 text-sm text-brand-muted">Acknowledged {formatAdminWhen(error.acknowledgedAt)}</p>
      ) : (
        <Button
          variant="outlined"
          onClick={onAcknowledge}
          disabled={acknowledging}
          sx={{ mt: 2, minHeight: 44, borderRadius: "14px" }}
        >
          {acknowledging ? "Acknowledging…" : "Acknowledge"}
        </Button>
      )}
    </article>
  );
}

import { Button, CircularProgress } from "@mui/material";
import type { CreditBatch } from "@/features/credits/types";
import type { PurchaseStage } from "@/features/credits/utils/purchase-stage";
import { formatSlotDay } from "@/utils/clock";

type PurchaseStatusProps = {
  stage: PurchaseStage;
  batch: CreditBatch | null;
  onFindInstructor: () => void;
  onDismiss: () => void;
};

export function PurchaseStatus({ stage, batch, onFindInstructor, onDismiss }: PurchaseStatusProps) {
  if (stage === "idle") return null;

  if (stage === "confirming") {
    return (
      <section className="flex items-center gap-4 rounded-3xl border border-brand-border bg-brand-surface p-5" role="status">
        <CircularProgress size={28} aria-hidden="true" />
        <div>
          <h2 className="font-heading text-lg font-medium">Confirming your payment…</h2>
          <p className="text-sm text-brand-muted">Your credits appear here as soon as the payment is confirmed.</p>
        </div>
      </section>
    );
  }

  if (stage === "refunded") {
    return (
      <section className="rounded-3xl border border-red-200 bg-brand-surface p-5" role="alert">
        <h2 className="font-heading text-lg font-medium">We couldn’t add this pack</h2>
        <p className="mt-1 text-sm text-brand-muted">Your payment has been refunded.</p>
        <Button variant="text" sx={{ mt: 1, px: 0 }} onClick={onDismiss}>
          Close
        </Button>
      </section>
    );
  }

  return (
    <section className="rounded-3xl border border-brand-primary bg-brand-surface p-5" role="status">
      <h2 className="font-heading text-lg font-medium">
        {batch ? `${batch.remaining} credits added` : "Credits added"}
      </h2>
      {batch ? (
        <p className="mt-1 text-sm text-brand-muted">
          {batch.packName}. Use them by {formatSlotDay(batch.useBy)}.
        </p>
      ) : null}
      <div className="mt-4 flex flex-col gap-2 sm:flex-row">
        <Button variant="contained" onClick={onFindInstructor} sx={{ minHeight: 44, borderRadius: "14px" }}>
          Find an instructor
        </Button>
        <Button variant="text" onClick={onDismiss}>
          Close
        </Button>
      </div>
    </section>
  );
}

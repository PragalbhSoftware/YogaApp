import { useState, type ReactNode } from "react";
import { Button, Chip, Dialog, DialogContent } from "@mui/material";
import { toast } from "sonner";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { SettingsCard } from "@/features/admin/components/settings-card";
import { lateCancelOptions, optionLabel, payBasisOptions } from "@/features/admin/credit-labels";
import { useRefundCreditBatch, useUserCredits } from "@/features/admin/hooks/use-admin-credits";
import type { AdminCreditBatch } from "@/features/admin/types";
import { formatAdminWhen } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";
import { formatSlotDay } from "@/utils/clock";
import { formatInr } from "@/utils/money";

const statusLabels: Record<AdminCreditBatch["status"], string> = {
  Active: "Active",
  UsedUp: "Used up",
  Expired: "Expired",
  Refunded: "Refunded",
};

function canRefundBatch(batch: AdminCreditBatch) {
  return batch.status === "Active" && batch.refundsEnabled && batch.remaining > 0;
}

export function UserCreditsCard({ userId }: { userId: string }) {
  const credits = useUserCredits(userId);
  const refund = useRefundCreditBatch();
  const [refunding, setRefunding] = useState<AdminCreditBatch | null>(null);

  function confirmRefund(batch: AdminCreditBatch) {
    refund.mutate(batch.id, {
      onSuccess: (saved) => {
        setRefunding(null);
        toast.success(
          saved.refundedAmount > 0
            ? `Refunded ${formatInr(saved.refundedAmount)}. Unused credits removed.`
            : "Unused credits removed. Nothing to refund.",
        );
      },
      onError: (error) => toast.error(toUserMessage(error)),
    });
  }

  return (
    <SettingsCard className="space-y-4">
      <div>
        <h2 className="font-heading text-lg font-medium">Credit packs</h2>
        <p className="text-sm text-brand-muted">Purchases with the rules saved on each one.</p>
      </div>
      {credits.isLoading ? <BookingListSkeleton /> : null}
      {credits.isError ? (
        <ErrorState message={toUserMessage(credits.error)} onRetry={() => void credits.refetch()} />
      ) : null}
      {credits.isSuccess && credits.data.length === 0 ? (
        <p className="text-sm text-brand-muted">No packs bought.</p>
      ) : null}
      {credits.isSuccess && credits.data.length > 0 ? (
        <ul className="space-y-3">
          {credits.data.map((batch) => (
            <BatchItem key={batch.id} batch={batch} busy={refund.isPending} onRefund={setRefunding} />
          ))}
        </ul>
      ) : null}
      <RefundDialog
        batch={refunding}
        busy={refund.isPending}
        onClose={() => setRefunding(null)}
        onConfirm={confirmRefund}
      />
    </SettingsCard>
  );
}

type BatchItemProps = {
  batch: AdminCreditBatch;
  busy: boolean;
  onRefund: (batch: AdminCreditBatch) => void;
};

function BatchItem({ batch, busy, onRefund }: BatchItemProps) {
  return (
    <li className="space-y-3 rounded-2xl border border-brand-border p-4 text-sm">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="font-medium">{batch.packName}</p>
        <Chip size="small" label={statusLabels[batch.status]} />
      </div>
      <dl className="grid gap-2 sm:grid-cols-2">
        <Fact label="Credits">
          {batch.remaining} left of {batch.sessions} · {batch.used} used
        </Fact>
        <Fact label="Paid">{formatInr(batch.price)}</Fact>
        <Fact label="Bought">{formatAdminWhen(batch.purchasedAt)}</Fact>
        <Fact label="Use by">{formatSlotDay(batch.useBy)}</Fact>
        <Fact label="Cap">{formatInr(batch.priceCap)} a session</Fact>
        {batch.refundedAmount > 0 ? <Fact label="Refunded">{formatInr(batch.refundedAmount)}</Fact> : null}
        <Fact label="Instructor pay">{optionLabel(payBasisOptions, batch.payBasis)}</Fact>
        <Fact label="Late cancel">{optionLabel(lateCancelOptions, batch.lateCancel)}</Fact>
        <Fact label="Convenience fee">{batch.chargeConvenienceFee ? "Charged" : "Not charged"}</Fact>
        <Fact label="After expiry">
          {batch.bookingStandsAfterExpiry ? "Booked sessions stand" : "Booked sessions blocked"}
        </Fact>
      </dl>
      {canRefundBatch(batch) ? (
        <Button
          variant="outlined"
          color="warning"
          disabled={busy}
          onClick={() => onRefund(batch)}
          sx={{ minHeight: 44, borderRadius: "14px" }}
        >
          Refund unused · {formatInr(batch.refundPreview)}
        </Button>
      ) : null}
    </li>
  );
}

type RefundDialogProps = {
  batch: AdminCreditBatch | null;
  busy: boolean;
  onClose: () => void;
  onConfirm: (batch: AdminCreditBatch) => void;
};

function RefundDialog({ batch, busy, onClose, onConfirm }: RefundDialogProps) {
  return (
    <Dialog
      open={Boolean(batch)}
      onClose={() => {
        if (!busy) onClose();
      }}
      fullWidth
      maxWidth="sm"
      slotProps={{ paper: { sx: { borderRadius: "24px" } } }}
    >
      {batch ? (
        <DialogContent sx={{ p: 3 }}>
          <div className="flex flex-col gap-5">
            <div>
              <h2 className="font-heading text-lg font-medium">Refund unused credits?</h2>
              <p className="mt-1 text-sm leading-relaxed text-brand-muted">
                The {batch.remaining} unused credits are removed. Upcoming bookings made with this pack stay booked.
              </p>
            </div>
            <dl className="flex flex-col gap-2 rounded-2xl border border-brand-border bg-brand-background p-4 text-sm">
              <div className="flex justify-between gap-3">
                <dt className="text-brand-muted">
                  {batch.remaining} × {formatInr(batch.referenceSessionPrice)}
                </dt>
                <dd className="tabular-nums">{formatInr(batch.remaining * batch.referenceSessionPrice)}</dd>
              </div>
              {batch.refundFee > 0 ? (
                <div className="flex justify-between gap-3">
                  <dt className="text-brand-muted">Refund fee</dt>
                  <dd className="tabular-nums">− {formatInr(batch.refundFee)}</dd>
                </div>
              ) : null}
              <div className="flex justify-between gap-3 border-t border-brand-border pt-2 font-medium">
                <dt>Refund (capped at {formatInr(batch.price - batch.refundedAmount)})</dt>
                <dd className="tabular-nums">{formatInr(batch.refundPreview)}</dd>
              </div>
            </dl>
            <div className="flex flex-col gap-2">
              <Button
                variant="contained"
                color="warning"
                disabled={busy}
                onClick={() => onConfirm(batch)}
                sx={{ minHeight: 48, borderRadius: "14px" }}
              >
                {busy ? "Refunding…" : `Refund ${formatInr(batch.refundPreview)}`}
              </Button>
              <Button variant="text" disabled={busy} onClick={onClose} sx={{ minHeight: 44 }}>
                Keep the pack
              </Button>
            </div>
          </div>
        </DialogContent>
      ) : null}
    </Dialog>
  );
}

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt className="text-brand-muted">{label}</dt>
      <dd>{children}</dd>
    </div>
  );
}

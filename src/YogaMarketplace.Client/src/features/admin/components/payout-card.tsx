import { useState } from "react";
import { Link } from "react-router-dom";
import { Button, Dialog, DialogActions, DialogContent, DialogTitle } from "@mui/material";
import { PriceText } from "@/components/common/price-text";
import { adminBookingPath } from "@/constants/routes";
import type { AdminPayout } from "@/features/admin/types";
import { formatAdminWhen } from "@/features/admin/utils";
import { payoutReasonLabel } from "@/features/bookings/utils/status";
import { formatInr } from "@/utils/money";

type PayoutCardProps = {
  payout: AdminPayout;
  busy: boolean;
  onMarkPaid: (id: string) => void;
};

export function PayoutCard({ payout, busy, onMarkPaid }: PayoutCardProps) {
  const [confirmOpen, setConfirmOpen] = useState(false);
  const canMarkPaid = payout.status === "Pending" || payout.status === "Exported";

  return (
    <article className="rounded-[24px] border border-brand-border bg-brand-surface p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="font-heading text-lg font-medium">{payout.providerName}</h2>
          <p className="mt-1 text-sm text-brand-muted">
            {payoutReasonLabel(payout.bookingStatus)} · {formatAdminWhen(payout.createdAt)}
          </p>
        </div>
        <p className="text-[11px] font-semibold tracking-[0.14em] text-brand-primary uppercase">
          {payout.status}
        </p>
      </div>

      <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-3">
        <div>
          <dt className="text-brand-muted">
            {payout.bookingStatus === "Cancelled" ? "Late-cancel fee" : "Customer paid"}
          </dt>
          <dd>
            <PriceText amount={payout.grossAmount} className="min-w-0 text-sm" />
          </dd>
        </div>
        <div>
          <dt className="text-brand-muted">Your take ({payout.feePercent}%)</dt>
          <dd>
            <PriceText amount={payout.feeAmount} className="min-w-0 text-sm" />
          </dd>
        </div>
        <div>
          <dt className="text-brand-muted">Pay instructor</dt>
          <dd>
            <PriceText amount={payout.netAmount} className="min-w-0 text-sm" />
          </dd>
        </div>
      </dl>

      <div className="mt-4 flex flex-wrap items-center gap-2">
        <Button
          component={Link}
          to={adminBookingPath(payout.bookingId)}
          variant="text"
          sx={{ minHeight: 44, px: 0 }}
        >
          View booking
        </Button>
        {canMarkPaid ? (
          <Button
            variant="contained"
            disabled={busy}
            onClick={() => setConfirmOpen(true)}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            Mark paid
          </Button>
        ) : null}
      </div>

      <Dialog
        open={confirmOpen}
        onClose={() => {
          if (!busy) setConfirmOpen(false);
        }}
        fullWidth
        maxWidth="sm"
        transitionDuration={0}
        slotProps={{
          backdrop: { sx: { bgcolor: "rgba(37, 49, 39, 0.45)" } },
          paper: { sx: { borderRadius: "24px", bgcolor: "#FFFFFF", backgroundImage: "none" } },
        }}
      >
        <DialogTitle sx={{ fontFamily: "inherit" }}>Mark this payout paid?</DialogTitle>
        <DialogContent>
          <p className="text-sm leading-relaxed text-brand-muted">
            You keep {formatInr(payout.feeAmount)} (your take). Send {formatInr(payout.netAmount)} to{" "}
            {payout.providerName} outside the app. This only records that you paid — it does not
            transfer money.
          </p>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2.5 }}>
          <Button variant="text" disabled={busy} onClick={() => setConfirmOpen(false)}>
            Cancel
          </Button>
          <Button
            variant="contained"
            disabled={busy}
            onClick={() => {
              onMarkPaid(payout.id);
              setConfirmOpen(false);
            }}
          >
            Mark paid
          </Button>
        </DialogActions>
      </Dialog>
    </article>
  );
}

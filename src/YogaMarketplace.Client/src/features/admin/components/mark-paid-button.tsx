import { useState } from "react";
import { Button, Dialog, DialogActions, DialogContent, DialogTitle } from "@mui/material";
import type { AdminPayout } from "@/features/admin/types";
import { formatInr } from "@/utils/money";

type MarkPaidButtonProps = {
  payout: AdminPayout;
  busy: boolean;
  onConfirm: (id: string) => void;
};

export function MarkPaidButton({ payout, busy, onConfirm }: MarkPaidButtonProps) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <Button
        variant="contained"
        size="small"
        disabled={busy}
        onClick={() => setOpen(true)}
        sx={{ minHeight: 36, borderRadius: "12px", whiteSpace: "nowrap" }}
      >
        Mark paid
      </Button>
      <Dialog
        open={open}
        onClose={() => {
          if (!busy) setOpen(false);
        }}
        fullWidth
        maxWidth="sm"
        transitionDuration={0}
        slotProps={{
          backdrop: { sx: { bgcolor: "rgba(37, 49, 39, 0.45)" } },
          paper: { sx: { borderRadius: "24px", bgcolor: "background.paper", backgroundImage: "none" } },
        }}
      >
        <DialogTitle sx={{ fontFamily: "inherit" }}>Mark this payout paid?</DialogTitle>
        <DialogContent>
          <p className="text-sm leading-relaxed text-brand-muted">
            You keep {formatInr(payout.feeAmount)} (your take). Send {formatInr(payout.netAmount)} to{" "}
            {payout.providerName} outside the app. This only records that you paid — it does not transfer money.
          </p>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2.5 }}>
          <Button variant="text" disabled={busy} onClick={() => setOpen(false)}>
            Cancel
          </Button>
          <Button
            variant="contained"
            disabled={busy}
            onClick={() => {
              onConfirm(payout.id);
              setOpen(false);
            }}
          >
            Mark paid
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}

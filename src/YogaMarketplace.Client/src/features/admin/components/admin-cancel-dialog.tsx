import { zodResolver } from "@hookform/resolvers/zod";
import { Button, Dialog, DialogContent, TextField } from "@mui/material";
import { useForm } from "react-hook-form";
import { adminCancelSchema, type AdminCancelValues } from "@/features/admin/schemas";
import type { AdminBooking } from "@/features/admin/types";
import { formatInr } from "@/utils/money";

type AdminCancelDialogProps = {
  booking: AdminBooking;
  open: boolean;
  busy: boolean;
  onClose: () => void;
  onConfirm: (reason: string) => void;
};

export function AdminCancelDialog({ booking, open, busy, onClose, onConfirm }: AdminCancelDialogProps) {
  const paid = booking.amount + booking.convenienceFee;
  const form = useForm<AdminCancelValues>({
    resolver: zodResolver(adminCancelSchema),
    defaultValues: { reason: "" },
  });

  const close = () => {
    if (busy) return;
    form.reset();
    onClose();
  };

  return (
    <Dialog
      open={open}
      onClose={close}
      fullWidth
      maxWidth="sm"
      slotProps={{ paper: { sx: { borderRadius: "24px" } } }}
    >
      <DialogContent sx={{ p: 3 }}>
        <form
          noValidate
          className="flex flex-col gap-5"
          onSubmit={form.handleSubmit((values) => onConfirm(values.reason.trim()))}
        >
          <div>
            <h2 className="font-heading text-lg font-medium">Cancel on the customer’s behalf?</h2>
            <p className="mt-1 text-sm leading-relaxed text-brand-muted">
              {booking.customerName ?? "The customer"} gets a full refund of {formatInr(paid)}, whatever the
              timing. {booking.providerName} earns nothing for this session and the slot opens up again.
            </p>
          </div>

          <TextField
            label="Reason"
            placeholder="For example: instructor unwell, customer asked on WhatsApp"
            multiline
            minRows={3}
            autoFocus
            disabled={busy}
            {...form.register("reason")}
            error={Boolean(form.formState.errors.reason)}
            helperText={form.formState.errors.reason?.message ?? "The customer sees this reason on their booking."}
          />

          <div className="flex flex-col gap-2">
            <Button
              type="submit"
              variant="contained"
              color="warning"
              disabled={busy}
              sx={{ minHeight: 48, borderRadius: "14px" }}
            >
              {busy ? "Cancelling…" : `Cancel and refund ${formatInr(paid)}`}
            </Button>
            <Button variant="text" disabled={busy} onClick={close} sx={{ minHeight: 44 }}>
              Keep booking
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

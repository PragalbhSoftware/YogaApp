import { zodResolver } from "@hookform/resolvers/zod";
import { Button, Dialog, DialogContent, TextField } from "@mui/material";
import { useForm } from "react-hook-form";
import { resolveItemSchema, type ResolveItemValues } from "@/features/admin/schemas";

type ReconciliationResolveDialogProps = {
  open: boolean;
  busy: boolean;
  onClose: () => void;
  onConfirm: (note: string) => void;
};

export function ReconciliationResolveDialog({ open, busy, onClose, onConfirm }: ReconciliationResolveDialogProps) {
  const form = useForm<ResolveItemValues>({
    resolver: zodResolver(resolveItemSchema),
    defaultValues: { note: "" },
  });

  const close = () => {
    if (busy) return;
    form.reset();
    onClose();
  };

  return (
    <Dialog open={open} onClose={close} fullWidth maxWidth="sm" slotProps={{ paper: { sx: { borderRadius: "24px" } } }}>
      <DialogContent sx={{ p: 3 }}>
        <form
          noValidate
          className="flex flex-col gap-5"
          onSubmit={form.handleSubmit((values) => onConfirm(values.note.trim()))}
        >
          <div>
            <h2 className="font-heading text-lg font-medium">Mark this resolved?</h2>
            <p className="mt-1 text-sm leading-relaxed text-brand-muted">
              Fix the money in Razorpay or with the customer first. This only records what you did.
            </p>
          </div>

          <TextField
            label="Note"
            placeholder="For example: refunded in the Razorpay dashboard, customer informed"
            multiline
            minRows={3}
            autoFocus
            disabled={busy}
            {...form.register("note")}
            error={Boolean(form.formState.errors.note)}
            helperText={form.formState.errors.note?.message ?? "Kept with the item for the audit trail."}
          />

          <div className="flex flex-col gap-2">
            <Button type="submit" variant="contained" disabled={busy} sx={{ minHeight: 48, borderRadius: "14px" }}>
              {busy ? "Saving…" : "Mark resolved"}
            </Button>
            <Button variant="text" disabled={busy} onClick={close} sx={{ minHeight: 44 }}>
              Keep open
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

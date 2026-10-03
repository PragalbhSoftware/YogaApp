import { useState } from "react";
import { Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField } from "@mui/material";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { BlockedBadge } from "@/features/admin/components/blocked-badge";
import { userBlockSchema, type UserBlockValues } from "@/features/admin/schemas";
import type { AdminUserDetail } from "@/features/admin/types";
import { displayName, formatAdminWhen } from "@/features/admin/utils";

type AccountStatusCardProps = {
  user: AdminUserDetail;
  busy: boolean;
  onChange: (blocked: boolean, reason?: string) => void;
};

export function AccountStatusCard({ user, busy, onChange }: AccountStatusCardProps) {
  const [open, setOpen] = useState(false);
  const blocking = !user.isBlocked;
  const isProvider = user.role === "Provider";
  const canBlock = user.role !== "Admin";
  const form = useForm<UserBlockValues>({
    resolver: zodResolver(userBlockSchema(blocking)),
    defaultValues: { reason: "" },
  });

  const close = () => {
    if (busy) return;
    setOpen(false);
    form.reset({ reason: "" });
  };

  if (!canBlock && user.blockHistory.length === 0) return null;

  return (
    <article className="space-y-4 rounded-[24px] border border-brand-border bg-brand-surface p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="font-heading text-xl font-medium">Account status</h2>
          <p className="mt-1 text-sm text-brand-muted">
            {user.isBlocked
              ? `Blocked ${user.blockedAt ? formatAdminWhen(user.blockedAt) : ""}${user.blockedReason ? ` · ${user.blockedReason}` : ""}`
              : "Active. Can sign in and book."}
          </p>
        </div>
        {user.isBlocked ? (
          <BlockedBadge />
        ) : (
          <p className="text-[11px] font-semibold tracking-[0.14em] text-brand-primary uppercase">Active</p>
        )}
      </div>

      {canBlock ? (
        <Button
          variant={user.isBlocked ? "contained" : "outlined"}
          color={user.isBlocked ? "primary" : "warning"}
          disabled={busy}
          onClick={() => setOpen(true)}
          sx={{ minHeight: 44, borderRadius: "14px" }}
        >
          {user.isBlocked ? "Unblock account" : "Block account"}
        </Button>
      ) : null}

      {user.blockHistory.length > 0 ? (
        <div>
          <h3 className="text-sm font-medium">History</h3>
          <ul className="mt-2 space-y-2 text-sm">
            {user.blockHistory.map((entry) => (
              <li key={entry.id} className="rounded-2xl bg-brand-background px-4 py-3">
                <span className="font-medium">{entry.action}</span>
                <span className="text-brand-muted">
                  {" "}
                  · {formatAdminWhen(entry.createdAt)} · by {entry.adminName ?? "an admin"}
                </span>
                {entry.reason ? <p className="mt-1 text-brand-muted">{entry.reason}</p> : null}
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      <Dialog
        open={open}
        onClose={close}
        fullWidth
        maxWidth="sm"
        slotProps={{ paper: { sx: { borderRadius: "24px" } } }}
      >
        <form
          noValidate
          onSubmit={form.handleSubmit((values) => {
            onChange(blocking, values.reason.trim() || undefined);
            setOpen(false);
            form.reset({ reason: "" });
          })}
        >
          <DialogTitle sx={{ fontFamily: "inherit" }}>
            {blocking ? "Block" : "Unblock"} {displayName(user.name, user.phone)}?
          </DialogTitle>
          <DialogContent>
            <p className="mb-4 text-sm leading-relaxed text-brand-muted">
              {blocking
                ? `They are signed out and cannot sign in again.${
                    isProvider ? " Their profile leaves public browse and nobody can book them." : ""
                  } Existing bookings stay as they are; cancel them one by one if needed.`
                : "They can sign in again straight away."}
            </p>
            <TextField
              label={blocking ? "Reason" : "Reason (optional)"}
              fullWidth
              multiline
              minRows={3}
              autoFocus
              disabled={busy}
              error={Boolean(form.formState.errors.reason)}
              helperText={form.formState.errors.reason?.message ?? "Kept in the account history. Not shown to the user."}
              slotProps={{ htmlInput: { maxLength: 300 } }}
              {...form.register("reason")}
            />
          </DialogContent>
          <DialogActions sx={{ px: 3, pb: 2.5 }}>
            <Button variant="text" disabled={busy} onClick={close}>
              Cancel
            </Button>
            <Button type="submit" color={blocking ? "warning" : "primary"} variant="contained" disabled={busy}>
              {blocking ? "Block" : "Unblock"}
            </Button>
          </DialogActions>
        </form>
      </Dialog>
    </article>
  );
}

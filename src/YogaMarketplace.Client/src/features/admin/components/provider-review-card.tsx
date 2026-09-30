import { useState } from "react";
import { Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField } from "@mui/material";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { PhoneText } from "@/components/common/phone-text";
import { rejectSchema, type RejectFormValues } from "@/features/admin/schemas";
import type { AdminProvider } from "@/features/admin/types";
import { formatAdminWhen, httpHref, modeRateLabel, offeredModes } from "@/features/admin/utils";

type ProviderReviewCardProps = {
  provider: AdminProvider;
  busy: boolean;
  onVerify: (id: string) => void;
  onReject: (id: string, reason?: string) => void;
};

export function ProviderReviewCard({ provider, busy, onVerify, onReject }: ProviderReviewCardProps) {
  const [rejectOpen, setRejectOpen] = useState(false);
  const canVerify = provider.status !== "Verified";
  const canReject = provider.status !== "Rejected";
  const meet = httpHref(provider.googleMeetLink);
  const form = useForm<RejectFormValues>({
    resolver: zodResolver(rejectSchema),
    defaultValues: { reason: "" },
  });

  return (
    <article className="space-y-4 rounded-[24px] border border-brand-border bg-brand-surface p-5 shadow-[0_8px_24px_rgba(37,49,39,0.04)]">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="font-heading text-xl font-medium">{provider.displayName}</h2>
          <p className="mt-1 text-sm text-brand-muted">
            {provider.area} · {provider.city}
          </p>
        </div>
        <p className="text-[11px] font-semibold tracking-[0.14em] text-brand-primary uppercase">
          {provider.status}
        </p>
      </div>

      <dl className="grid gap-3 text-sm sm:grid-cols-2">
        <div>
          <dt className="text-brand-muted">Phone</dt>
          <dd>
            <PhoneText value={provider.phone} />
          </dd>
        </div>
        {provider.email ? (
          <div>
            <dt className="text-brand-muted">Email</dt>
            <dd>{provider.email}</dd>
          </div>
        ) : null}
        {provider.age ? (
          <div>
            <dt className="text-brand-muted">Age</dt>
            <dd>{provider.age}</dd>
          </div>
        ) : null}
        <div>
          <dt className="text-brand-muted">Submitted</dt>
          <dd>{formatAdminWhen(provider.createdAt)}</dd>
        </div>
      </dl>

      {provider.bio ? <p className="text-sm leading-relaxed text-brand-muted">{provider.bio}</p> : null}

      <ul className="flex flex-wrap gap-2">
        {offeredModes(provider).map((item) => (
          <li key={item.mode} className="rounded-full bg-brand-background px-3 py-1 text-sm">
            {modeRateLabel(item.mode, item.rate)}
          </li>
        ))}
      </ul>

      {provider.studioAddress ? (
        <p className="text-sm text-brand-muted">Studio · {provider.studioAddress}</p>
      ) : null}
      {meet ? (
        <a className="text-sm font-medium text-brand-primary" href={meet} target="_blank" rel="noreferrer">
          Google Meet link
        </a>
      ) : null}
      {provider.rejectionReason ? (
        <p className="text-sm text-brand-muted">Rejected: {provider.rejectionReason}</p>
      ) : null}

      {canVerify || canReject ? (
        <div className="flex flex-wrap gap-2">
          {canVerify ? (
            <Button
              variant="contained"
              disabled={busy}
              onClick={() => onVerify(provider.id)}
              sx={{ minHeight: 44, borderRadius: "14px" }}
            >
              Verify
            </Button>
          ) : null}
          {canReject ? (
            <Button
              variant="outlined"
              disabled={busy}
              onClick={() => setRejectOpen(true)}
              sx={{ minHeight: 44, borderRadius: "14px" }}
            >
              Reject
            </Button>
          ) : null}
        </div>
      ) : null}

      <Dialog
        open={rejectOpen}
        onClose={() => {
          if (!busy) setRejectOpen(false);
        }}
        fullWidth
        maxWidth="sm"
        transitionDuration={0}
        slotProps={{
          backdrop: { sx: { bgcolor: "rgba(37, 49, 39, 0.45)" } },
          paper: { sx: { borderRadius: "24px", bgcolor: "#FFFFFF", backgroundImage: "none" } },
        }}
      >
        <form
          onSubmit={form.handleSubmit((values) => {
            onReject(provider.id, values.reason?.trim() || undefined);
            setRejectOpen(false);
            form.reset({ reason: "" });
          })}
        >
          <DialogTitle sx={{ fontFamily: "inherit" }}>Reject {provider.displayName}?</DialogTitle>
          <DialogContent>
            <p className="mb-4 text-sm leading-relaxed text-brand-muted">
              They stay off public browse. You can verify them later.
            </p>
            <TextField
              label="Reason (optional)"
              fullWidth
              multiline
              minRows={3}
              error={Boolean(form.formState.errors.reason)}
              helperText={form.formState.errors.reason?.message}
              slotProps={{ htmlInput: { maxLength: 300 } }}
              {...form.register("reason")}
            />
          </DialogContent>
          <DialogActions sx={{ px: 3, pb: 2.5 }}>
            <Button variant="text" disabled={busy} onClick={() => setRejectOpen(false)}>
              Cancel
            </Button>
            <Button type="submit" color="warning" variant="contained" disabled={busy}>
              Reject
            </Button>
          </DialogActions>
        </form>
      </Dialog>
    </article>
  );
}

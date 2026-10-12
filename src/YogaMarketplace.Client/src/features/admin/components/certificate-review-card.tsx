import { useState } from "react";
import { Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField } from "@mui/material";
import { FileText } from "lucide-react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { certificateRejectSchema, type CertificateRejectValues } from "@/features/admin/schemas";
import type { AdminCertificate } from "@/features/admin/types";
import { formatAdminWhen } from "@/features/admin/utils";
import { formatFileSize } from "@/features/instructor/media-rules";

type CertificateReviewCardProps = {
  certificate: AdminCertificate;
  busy: boolean;
  onOpen: (certificate: AdminCertificate) => void;
  onApprove: (id: string) => void;
  onReject: (id: string, reason: string) => void;
};

export function CertificateReviewCard({ certificate, busy, onOpen, onApprove, onReject }: CertificateReviewCardProps) {
  const [rejectOpen, setRejectOpen] = useState(false);
  const form = useForm<CertificateRejectValues>({
    resolver: zodResolver(certificateRejectSchema),
    defaultValues: { reason: "" },
  });

  return (
    <article className="space-y-4 rounded-[24px] border border-brand-border bg-brand-surface p-5 shadow-[0_8px_24px_rgba(37,49,39,0.04)]">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="font-heading text-xl font-medium">{certificate.title}</h2>
          <p className="mt-1 text-sm text-brand-muted">{certificate.providerName}</p>
        </div>
        <p className="text-[11px] font-semibold tracking-[0.14em] text-brand-primary uppercase">
          {certificate.status}
        </p>
      </div>

      <dl className="grid gap-3 text-sm sm:grid-cols-2">
        <div>
          <dt className="text-brand-muted">Uploaded</dt>
          <dd>{formatAdminWhen(certificate.createdAt)}</dd>
        </div>
        <div>
          <dt className="text-brand-muted">File</dt>
          <dd>
            {certificate.contentType === "application/pdf" ? "PDF" : "Image"} · {formatFileSize(certificate.sizeBytes)}
          </dd>
        </div>
      </dl>

      {certificate.rejectionReason ? (
        <p className="text-sm text-brand-muted">Rejected: {certificate.rejectionReason}</p>
      ) : null}

      <div className="flex flex-wrap gap-2">
        <Button
          variant="text"
          startIcon={<FileText className="size-4" aria-hidden="true" />}
          onClick={() => onOpen(certificate)}
          sx={{ minHeight: 44, borderRadius: "14px" }}
        >
          Download file
        </Button>
        {certificate.status !== "Approved" ? (
          <Button
            variant="contained"
            disabled={busy}
            onClick={() => onApprove(certificate.id)}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            Approve
          </Button>
        ) : null}
        {certificate.status !== "Rejected" ? (
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
            onReject(certificate.id, values.reason);
            setRejectOpen(false);
            form.reset({ reason: "" });
          })}
        >
          <DialogTitle sx={{ fontFamily: "inherit" }}>Reject “{certificate.title}”?</DialogTitle>
          <DialogContent>
            <p className="mb-4 text-sm leading-relaxed text-brand-muted">
              The instructor sees your reason and can upload a clearer copy.
            </p>
            <TextField
              label="Reason"
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

import { useState, type ReactNode } from "react";
import { Button, Chip, IconButton, TextField } from "@mui/material";
import { FileText, FileUp, Trash2 } from "lucide-react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { FileDropZone } from "@/features/instructor/components/file-drop-zone";
import { MediaHeading } from "@/features/instructor/components/media-heading";
import { instructorApi } from "@/features/instructor/api/instructor-api";
import { useAddCertificate, useDeleteCertificate } from "@/features/instructor/hooks/use-instructor-schedule";
import {
  certificateFileName,
  certificateRules,
  certificateSchema,
  fileProblem,
  formatFileSize,
  type CertificateFormValues,
} from "@/features/instructor/media-rules";
import type { CertificateStatus, ProviderCertificate } from "@/features/instructor/types";
import { toUserMessage } from "@/services/http/api-error";
import { saveBlob } from "@/utils/download";

const statusLabel: Record<CertificateStatus, { label: string; color: "default" | "success" | "warning" }> = {
  Pending: { label: "Waiting for review", color: "default" },
  Approved: { label: "Approved", color: "success" },
  Rejected: { label: "Not approved", color: "warning" },
};

type CertificateManagerProps = {
  certificates: ProviderCertificate[];
  badge?: ReactNode;
};

export function CertificateManager({ certificates, badge }: CertificateManagerProps) {
  const addCertificate = useAddCertificate();
  const deleteCertificate = useDeleteCertificate();
  const [file, setFile] = useState<File | null>(null);
  const form = useForm<CertificateFormValues>({
    resolver: zodResolver(certificateSchema),
    defaultValues: { title: "" },
  });
  const full = certificates.length >= certificateRules.max;

  function pick(next: File) {
    const problem = fileProblem(next, certificateRules);
    if (problem) {
      toast.error(problem);
      return;
    }
    setFile(next);
  }

  async function view(certificate: ProviderCertificate) {
    try {
      saveBlob(
        await instructorApi.certificateFile(certificate.id),
        certificateFileName(certificate.title, certificate.contentType),
      );
    } catch (error) {
      toast.error(toUserMessage(error));
    }
  }

  return (
    <section className="flex flex-col gap-4 rounded-3xl border border-brand-border bg-brand-surface p-5 sm:p-6">
      <MediaHeading
        level="card"
        title="Certificates"
        badge={badge}
        caption="PDF, JPEG or PNG · up to 10 MB each · private to you and our team. Customers see the title and a Verified badge once we approve one."
        count={{ used: certificates.length, max: certificateRules.max }}
      />

      {certificates.length > 0 ? (
        <ul className="space-y-2">
          {certificates.map((certificate) => {
            const status = statusLabel[certificate.status];
            return (
              <li key={certificate.id} className="flex items-start gap-3 rounded-2xl border border-brand-border p-3">
                <FileText className="mt-0.5 size-5 shrink-0 text-brand-primary" aria-hidden="true" />
                <div className="min-w-0 flex-1 space-y-1">
                  <button
                    type="button"
                    className="text-left text-sm font-medium text-brand-text underline-offset-2 hover:underline"
                    onClick={() => void view(certificate)}
                  >
                    {certificate.title}
                  </button>
                  <div className="flex flex-wrap items-center gap-2 text-xs text-brand-muted">
                    <Chip size="small" label={status.label} color={status.color} />
                    {formatFileSize(certificate.sizeBytes)}
                  </div>
                  {certificate.rejectionReason ? (
                    <p className="text-sm text-brand-muted">Reason: {certificate.rejectionReason}</p>
                  ) : null}
                </div>
                <IconButton
                  aria-label={`Remove ${certificate.title}`}
                  disabled={deleteCertificate.isPending}
                  onClick={() =>
                    deleteCertificate.mutate(certificate.id, {
                      onSuccess: () => toast.success("Certificate removed."),
                      onError: (error) => toast.error(toUserMessage(error)),
                    })
                  }
                  size="small"
                >
                  <Trash2 className="size-4" aria-hidden="true" />
                </IconButton>
              </li>
            );
          })}
        </ul>
      ) : null}

      {full ? (
        <p className="text-sm text-brand-muted">Remove a certificate to upload another.</p>
      ) : (
        <form
          className="flex flex-col gap-3"
          noValidate
          onSubmit={form.handleSubmit((values) => {
            if (!file) {
              toast.error("Choose a file to upload.");
              return;
            }
            addCertificate.mutate(
              { title: values.title, file },
              {
                onSuccess: () => {
                  toast.success("Certificate sent for review.");
                  form.reset({ title: "" });
                  setFile(null);
                },
                onError: (error) => toast.error(toUserMessage(error)),
              },
            );
          })}
        >
          <FileDropZone
            label={file ? `Change file, ${file.name} chosen` : "Choose a certificate file"}
            accept={certificateRules.accept}
            disabled={addCertificate.isPending}
            onPick={pick}
            className="w-full px-4 py-5"
          >
            {file ? (
              <>
                <FileText className="size-6 text-brand-primary" aria-hidden="true" />
                <span className="max-w-full truncate text-sm font-medium text-brand-text">{file.name}</span>
                <span className="text-xs text-brand-muted">{formatFileSize(file.size)} · click to change</span>
              </>
            ) : (
              <>
                <FileUp className="size-6 text-brand-primary" aria-hidden="true" />
                <span className="text-sm font-medium text-brand-text">Choose a file</span>
                <span className="text-xs text-brand-muted">or drop it here</span>
              </>
            )}
          </FileDropZone>
          <TextField
            label="Certificate title"
            placeholder="For example, 200-hour teacher training"
            fullWidth
            error={Boolean(form.formState.errors.title)}
            helperText={form.formState.errors.title?.message}
            slotProps={{ htmlInput: { maxLength: 120 } }}
            {...form.register("title")}
          />
          <Button
            type="submit"
            variant="outlined"
            fullWidth
            disabled={addCertificate.isPending || !file}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            {addCertificate.isPending ? "Uploading…" : "Send for review"}
          </Button>
        </form>
      )}
    </section>
  );
}

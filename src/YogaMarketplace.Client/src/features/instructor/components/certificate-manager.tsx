import { useState } from "react";
import { Button, Chip, IconButton, TextField } from "@mui/material";
import { FileText, Trash2 } from "lucide-react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { FilePickButton } from "@/features/instructor/components/file-pick-button";
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
};

export function CertificateManager({ certificates }: CertificateManagerProps) {
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
    <section className="space-y-3">
      <div className="space-y-1">
        <h3 className="font-heading text-base font-medium">Certificates</h3>
        <p className="text-sm text-brand-muted">
          Up to {certificateRules.max} files, PDF, JPEG or PNG, 10 MB each. Only you and our admin team can open them.
          Customers see the title and a Verified badge after we approve one.
        </p>
      </div>

      {certificates.length > 0 ? (
        <ul className="space-y-2">
          {certificates.map((certificate) => {
            const status = statusLabel[certificate.status];
            return (
              <li
                key={certificate.id}
                className="flex items-start gap-3 rounded-2xl border border-brand-border p-3"
              >
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
          className="space-y-3"
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
          <TextField
            label="Certificate title"
            placeholder="For example, 200-hour teacher training"
            fullWidth
            error={Boolean(form.formState.errors.title)}
            helperText={form.formState.errors.title?.message}
            slotProps={{ htmlInput: { maxLength: 120 } }}
            {...form.register("title")}
          />
          <div className="flex flex-wrap items-center gap-3">
            <FilePickButton
              label={file ? "Change file" : "Choose file"}
              accept={certificateRules.accept}
              disabled={addCertificate.isPending}
              onPick={pick}
            />
            <span className="min-w-0 truncate text-sm text-brand-muted">{file ? file.name : "No file chosen"}</span>
          </div>
          <Button
            type="submit"
            variant="contained"
            disabled={addCertificate.isPending}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            {addCertificate.isPending ? "Uploading…" : "Upload certificate"}
          </Button>
        </form>
      )}
    </section>
  );
}

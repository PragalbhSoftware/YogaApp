import { Button } from "@mui/material";
import { Download, FileOutput } from "lucide-react";
import { toast } from "sonner";
import { useDownloadPayoutsCsv, useExportPayouts } from "@/features/admin/hooks/use-admin";
import { toUserMessage } from "@/services/http/api-error";
import { saveBlob } from "@/utils/download";

type PayoutExportBarProps = {
  status: string;
  rowCount: number;
};

export function PayoutExportBar({ status, rowCount }: PayoutExportBarProps) {
  const exportPayouts = useExportPayouts();
  const download = useDownloadPayoutsCsv();
  const busy = exportPayouts.isPending || download.isPending;

  const runExport = () => {
    exportPayouts.mutate(undefined, {
      onSuccess: (file) => {
        saveBlob(file.blob, file.fileName);
        toast.success("Exported. Pay each instructor, then mark their payout paid.");
      },
      onError: (error) => toast.error(toUserMessage(error)),
    });
  };

  const runDownload = () => {
    download.mutate(status, {
      onSuccess: (file) => saveBlob(file.blob, file.fileName),
      onError: (error) => toast.error(toUserMessage(error)),
    });
  };

  return (
    <section
      aria-label="Payout export"
      className="flex flex-col gap-3 rounded-[24px] border border-brand-border bg-brand-surface p-5 sm:flex-row sm:items-center sm:justify-between"
    >
      <p className="text-sm leading-relaxed text-brand-muted">
        Export moves every pending payout to <span className="font-medium text-brand-text">Exported</span> and
        downloads a CSV with each instructor’s phone and amount. Pay by bank or UPI, then mark each one paid.
      </p>
      <div className="flex shrink-0 flex-wrap gap-2">
        {status === "Pending" ? (
          <Button
            variant="contained"
            startIcon={<FileOutput className="size-4" />}
            disabled={busy || rowCount === 0}
            onClick={runExport}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            {exportPayouts.isPending ? "Exporting…" : "Export pending"}
          </Button>
        ) : null}
        <Button
          variant="outlined"
          startIcon={<Download className="size-4" />}
          disabled={busy || rowCount === 0}
          onClick={runDownload}
          sx={{ minHeight: 44, borderRadius: "14px" }}
        >
          {download.isPending ? "Preparing…" : "Download CSV"}
        </Button>
      </div>
    </section>
  );
}

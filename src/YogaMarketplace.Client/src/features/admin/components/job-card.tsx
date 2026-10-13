import { Button } from "@mui/material";
import type { AdminJob } from "@/features/admin/types";
import { formatAdminWhen, jobLabel } from "@/features/admin/utils";

function timing(job: AdminJob) {
  if (job.completedAt) return `${job.status === "Failed" ? "Failed" : "Finished"} ${formatAdminWhen(job.completedAt)}`;
  if (job.status === "Running") return `Started for ${formatAdminWhen(job.runAt)}`;
  return `${job.attempts > 0 ? "Retries" : "Runs"} ${formatAdminWhen(job.runAt)}`;
}

type JobCardProps = {
  job: AdminJob;
  retrying: boolean;
  onRetry: () => void;
};

export function JobCard({ job, retrying, onRetry }: JobCardProps) {
  return (
    <article className="rounded-[24px] border border-brand-border bg-brand-surface p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 className="font-heading text-lg font-medium">{jobLabel(job.type)}</h3>
          <p className="mt-1 text-sm text-brand-muted">
            {timing(job)} · Attempt {job.attempts} of {job.maxAttempts}
            {job.payload ? ` · ${job.payload}` : ""}
          </p>
        </div>
        <p className="text-[11px] font-semibold tracking-[0.14em] text-brand-primary uppercase">
          {job.recurring ? "Scheduled" : job.status}
        </p>
      </div>
      {job.lastError ? (
        <p className="mt-3 text-sm break-words">
          <span className="font-medium">Last error:</span> {job.lastError}
        </p>
      ) : null}
      {job.status === "Failed" ? (
        <Button
          variant="outlined"
          onClick={onRetry}
          disabled={retrying}
          sx={{ mt: 2, minHeight: 44, borderRadius: "14px" }}
        >
          {retrying ? "Retrying…" : "Retry now"}
        </Button>
      ) : null}
    </article>
  );
}

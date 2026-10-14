import { useSearchParams } from "react-router-dom";
import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { AdminPage } from "@/features/admin/components/admin-page";
import { FilterChips } from "@/features/admin/components/filter-chips";
import { JobCard } from "@/features/admin/components/job-card";
import { JobScheduleSettings } from "@/features/admin/components/job-schedule-settings";
import { ListPagination } from "@/features/admin/components/list-pagination";
import { useAdminJobs, useRetryJob } from "@/features/admin/hooks/use-admin";
import { backgroundJobStatuses, type BackgroundJobStatus } from "@/features/admin/types";
import { jobLabel } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";
import { toastError } from "@/services/http/toast-error";

const statusFilters = backgroundJobStatuses.map((status) => ({ value: status, label: status }));

const emptyCopy: Record<BackgroundJobStatus, string> = {
  Failed: "No job has failed. Failed jobs show here with their error so you can run them again.",
  Pending: "Nothing is waiting to run.",
  Running: "Nothing is running right now.",
  Succeeded: "No job has finished yet.",
};

function readStatus(value: string | null): BackgroundJobStatus {
  return backgroundJobStatuses.includes(value as BackgroundJobStatus) ? (value as BackgroundJobStatus) : "Failed";
}

function readPage(value: string | null) {
  const page = Number(value);
  return Number.isInteger(page) && page > 1 ? page : 1;
}

export function JobsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const status = readStatus(searchParams.get("status"));
  const page = readPage(searchParams.get("page"));
  const jobs = useAdminJobs(status, page);
  const retry = useRetryJob();

  function setParams(next: { status?: string; page?: number }) {
    const params = new URLSearchParams(searchParams);
    if (next.status !== undefined) {
      params.set("status", next.status);
      params.delete("page");
    }
    if (next.page !== undefined) {
      if (next.page > 1) params.set("page", String(next.page));
      else params.delete("page");
    }
    setSearchParams(params, { replace: true });
  }

  return (
    <AdminPage
      kicker="Owner"
      title="Jobs"
      lead="Work the app does in the background: daily reconciliation, credit expiry and webhook clean-up. A job that keeps failing stops here with its error until you retry it."
    >
      <div className="space-y-8">
        <JobScheduleSettings />

        <section className="space-y-3" aria-label="Job runs">
          <h2 className="font-heading text-lg font-medium">Runs</h2>
          <FilterChips
            value={status}
            options={statusFilters}
            onChange={(next) => setParams({ status: next ?? "Failed" })}
            label="Job status"
          />
          {jobs.isLoading ? <BookingListSkeleton /> : null}
          {jobs.isError ? <ErrorState message={toUserMessage(jobs.error)} onRetry={() => void jobs.refetch()} /> : null}
          {jobs.isSuccess && jobs.data.items.length === 0 ? (
            <EmptyState title={`No ${status.toLowerCase()} jobs`} description={emptyCopy[status]} />
          ) : null}
          {jobs.isSuccess
            ? jobs.data.items.map((job) => (
                <JobCard
                  key={job.id}
                  job={job}
                  retrying={retry.isPending && retry.variables === job.id}
                  onRetry={() =>
                    retry.mutate(job.id, {
                      onSuccess: () => toast.success(`${jobLabel(job.type)} queued to run now.`),
                      onError: (error) => toastError(error),
                    })
                  }
                />
              ))
            : null}
          {jobs.isSuccess ? (
            <ListPagination label="Job pages" list={jobs.data} onChange={(next) => setParams({ page: next })} />
          ) : null}
        </section>
      </div>
    </AdminPage>
  );
}

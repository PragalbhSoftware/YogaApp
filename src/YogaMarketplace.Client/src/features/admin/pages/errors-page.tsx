import { useSearchParams } from "react-router-dom";
import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { AdminPage } from "@/features/admin/components/admin-page";
import { ErrorEventCard } from "@/features/admin/components/error-event-card";
import { FilterChips } from "@/features/admin/components/filter-chips";
import { ListPagination } from "@/features/admin/components/list-pagination";
import { useAcknowledgeError, useAdminErrors } from "@/features/admin/hooks/use-admin";
import { useStepBackWhenEmpty } from "@/features/admin/hooks/use-step-back-when-empty";
import { toUserMessage } from "@/services/http/api-error";
import { toastError } from "@/services/http/toast-error";

const views = ["open", "acknowledged"] as const;
type ErrorView = (typeof views)[number];

const viewFilters = [
  { value: "open" as const, label: "Open" },
  { value: "acknowledged" as const, label: "Acknowledged" },
];

function readView(value: string | null): ErrorView {
  return views.includes(value as ErrorView) ? (value as ErrorView) : "open";
}

function readPage(value: string | null) {
  const page = Number(value);
  return Number.isInteger(page) && page > 1 ? page : 1;
}

export function ErrorsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const view = readView(searchParams.get("view"));
  const page = readPage(searchParams.get("page"));
  const errors = useAdminErrors(view === "acknowledged", page);
  const acknowledge = useAcknowledgeError();

  function setParams(next: { view?: string; page?: number }) {
    const params = new URLSearchParams(searchParams);
    if (next.view !== undefined) {
      params.set("view", next.view);
      params.delete("page");
    }
    if (next.page !== undefined) {
      if (next.page > 1) params.set("page", String(next.page));
      else params.delete("page");
    }
    setSearchParams(params, { replace: true });
  }

  useStepBackWhenEmpty(errors.data, page, (next) => setParams({ page: next }));

  return (
    <AdminPage
      kicker="Owner"
      title="Errors"
      lead="Server errors grouped by kind, newest first. Give the correlation id to your developer to find the matching log lines. Acknowledge an error once it is handled; it opens again if it happens again."
    >
      <section className="space-y-3" aria-label="Errors">
        <FilterChips
          value={view}
          options={viewFilters}
          onChange={(next) => setParams({ view: next ?? "open" })}
          label="Error status"
        />
        {errors.isLoading ? <BookingListSkeleton /> : null}
        {errors.isError ? <ErrorState message={toUserMessage(errors.error)} onRetry={() => void errors.refetch()} /> : null}
        {errors.isSuccess && errors.data.items.length === 0 ? (
          <EmptyState
            title={view === "open" ? "No open errors" : "Nothing acknowledged yet"}
            description={
              view === "open"
                ? "All quiet. New server errors show here as they happen."
                : "Errors you acknowledge move here until they happen again."
            }
          />
        ) : null}
        {errors.isSuccess
          ? errors.data.items.map((error) => (
              <ErrorEventCard
                key={error.id}
                error={error}
                acknowledging={acknowledge.isPending && acknowledge.variables === error.id}
                onAcknowledge={() =>
                  acknowledge.mutate(error.id, {
                    onSuccess: () => toast.success("Error acknowledged."),
                    onError: (failure) => toastError(failure),
                  })
                }
              />
            ))
          : null}
        {errors.isSuccess ? (
          <ListPagination label="Error pages" list={errors.data} onChange={(next) => setParams({ page: next })} />
        ) : null}
      </section>
    </AdminPage>
  );
}

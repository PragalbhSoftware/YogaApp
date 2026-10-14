import { useState } from "react";
import { Button } from "@mui/material";
import { useSearchParams } from "react-router-dom";
import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { FilterChips } from "@/features/admin/components/filter-chips";
import { ListPagination } from "@/features/admin/components/list-pagination";
import { ReconciliationResolveDialog } from "@/features/admin/components/reconciliation-resolve-dialog";
import {
  useReconciliationItems,
  useReconciliationRuns,
  useResolveReconciliationItem,
} from "@/features/admin/hooks/use-admin";
import {
  reconciliationItemStatuses,
  reconciliationItemTypes,
  type ReconciliationItem,
  type ReconciliationItemType,
  type ReconciliationRun,
} from "@/features/admin/types";
import { formatAdminWhen } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";
import { formatSlotDay } from "@/utils/clock";
import { toastError } from "@/services/http/toast-error";

const reconciliationTypeLabels: Record<ReconciliationItemType, string> = {
  MissingInDatabase: "Not in our records",
  MissingInRazorpay: "Not in Razorpay",
  AmountMismatch: "Amount differs",
  StatusMismatch: "Status differs",
  RefundMismatch: "Refund differs",
  RefundNotRecorded: "Our refund not recorded",
  DashboardRefund: "Dashboard refund",
  StuckWebhook: "Stuck webhook",
  RefundPending: "Refund pending",
};

const typeFilters = [
  { value: null, label: "All types" },
  ...reconciliationItemTypes.map((type) => ({ value: type, label: reconciliationTypeLabels[type] })),
];

const statusFilters = reconciliationItemStatuses.map((status) => ({ value: status, label: status }));

function pick<T extends string>(value: string | null, allowed: readonly T[]): T | undefined {
  return allowed.includes(value as T) ? (value as T) : undefined;
}

function pageParam(value: string | null) {
  const page = Number(value);
  return Number.isInteger(page) && page > 1 ? page : 1;
}

export function ReconciliationPanel() {
  const [searchParams, setSearchParams] = useSearchParams();
  const runId = searchParams.get("run") ?? undefined;
  const type = pick(searchParams.get("type"), reconciliationItemTypes);
  const status = pick(searchParams.get("state"), reconciliationItemStatuses) ?? "Open";
  const runsPage = pageParam(searchParams.get("runsPage"));
  const itemsPage = pageParam(searchParams.get("page"));
  const runs = useReconciliationRuns(runsPage);
  const items = useReconciliationItems({ runId, type, status, page: itemsPage });
  const resolve = useResolveReconciliationItem();
  const [resolving, setResolving] = useState<ReconciliationItem | null>(null);

  function setParam(key: string, value: string | null) {
    const params = new URLSearchParams(searchParams);
    if (value) params.set(key, value);
    else params.delete(key);
    if (key !== "page" && key !== "runsPage") params.delete("page");
    setSearchParams(params, { replace: true });
  }

  function setPage(key: "page" | "runsPage", page: number) {
    setParam(key, page > 1 ? String(page) : null);
  }

  return (
    <div className="space-y-6">
      <section className="space-y-3" aria-label="Daily runs">
        <h2 className="font-heading text-lg font-medium">Daily runs</h2>
        {runs.isLoading ? <BookingListSkeleton /> : null}
        {runs.isError ? <ErrorState message={toUserMessage(runs.error)} onRetry={() => void runs.refetch()} /> : null}
        {runs.isSuccess && runs.data.items.length === 0 ? (
          <EmptyState
            title="No runs yet"
            description="The first daily check runs at the time set under Settings › Fees and covers the day before."
          />
        ) : null}
        {runs.isSuccess && runs.data.items.length > 0 ? (
          <div className="grid gap-3 sm:grid-cols-2">
            {runs.data.items.map((run) => (
              <RunCard
                key={run.id}
                run={run}
                selected={run.id === runId}
                onSelect={() => setParam("run", run.id === runId ? null : run.id)}
              />
            ))}
          </div>
        ) : null}
        {runs.isSuccess ? (
          <ListPagination label="Daily runs pages" list={runs.data} onChange={(page) => setPage("runsPage", page)} />
        ) : null}
      </section>

      <section className="space-y-3" aria-label="Items to check">
        <h2 className="font-heading text-lg font-medium">Items to check</h2>
        <FilterChips
          value={status}
          options={statusFilters}
          onChange={(next) => setParam("state", next === "Open" ? null : next)}
          label="Item status"
        />
        <FilterChips value={type ?? null} options={typeFilters} onChange={(next) => setParam("type", next)} label="Item type" />
        {items.isLoading ? <BookingListSkeleton /> : null}
        {items.isError ? <ErrorState message={toUserMessage(items.error)} onRetry={() => void items.refetch()} /> : null}
        {items.isSuccess && items.data.items.length === 0 ? (
          <EmptyState
            title={status === "Open" ? "Nothing to check" : "Nothing resolved yet"}
            description="Razorpay and our records agree for the runs and filters selected."
          />
        ) : null}
        {items.isSuccess
          ? items.data.items.map((item) => <ItemCard key={item.id} item={item} onResolve={() => setResolving(item)} />)
          : null}
        {items.isSuccess ? (
          <ListPagination label="Items pages" list={items.data} onChange={(page) => setPage("page", page)} />
        ) : null}
      </section>

      <ReconciliationResolveDialog
        key={resolving?.id ?? "none"}
        open={resolving !== null}
        busy={resolve.isPending}
        onClose={() => setResolving(null)}
        onConfirm={(note) => {
          if (!resolving) return;
          resolve.mutate(
            { id: resolving.id, note },
            {
              onSuccess: () => {
                toast.success("Marked resolved.");
                setResolving(null);
              },
              onError: (error) => toastError(error),
            },
          );
        }}
      />
    </div>
  );
}

function runSummary(run: ReconciliationRun) {
  if (run.status === "Failed") return "Failed";
  if (run.status === "Superseded") return "Replaced by a later run";
  return run.itemCount === 0 ? "All clear" : `${run.openCount} open of ${run.itemCount}`;
}

function RunCard({ run, selected, onSelect }: { run: ReconciliationRun; selected: boolean; onSelect: () => void }) {
  const failed = run.status === "Failed";
  return (
    <article className="rounded-[24px] border border-brand-border bg-brand-surface p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="font-heading text-base font-medium">{formatSlotDay(run.marketDate)}</h3>
          <p className="mt-1 text-sm text-brand-muted">Checked {formatAdminWhen(run.startedAt)}</p>
        </div>
        <p className="text-[11px] font-semibold tracking-[0.14em] text-brand-primary uppercase">{runSummary(run)}</p>
      </div>
      {failed && run.error ? <p className="mt-2 text-sm text-brand-muted">{run.error} It retries automatically.</p> : null}
      {!run.gatewayCompared ? (
        <p className="mt-2 text-sm text-brand-muted">Razorpay comparison skipped (test gateway); database checks only.</p>
      ) : null}
      {run.itemCount > 0 ? (
        <Button variant="text" onClick={onSelect} aria-pressed={selected} sx={{ mt: 1, minHeight: 44, px: 0 }}>
          {selected ? "Show all runs" : "Show this run’s items"}
        </Button>
      ) : null}
    </article>
  );
}

function ItemCard({ item, onResolve }: { item: ReconciliationItem; onResolve: () => void }) {
  const resolved = item.status === "Resolved";
  return (
    <article className="rounded-[24px] border border-brand-border bg-brand-surface p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="font-heading text-lg font-medium">{reconciliationTypeLabels[item.type]}</h3>
          <p className="mt-1 text-sm text-brand-muted">
            {formatSlotDay(item.marketDate)} · {item.detail}
          </p>
        </div>
        <p className="text-[11px] font-semibold tracking-[0.14em] text-brand-primary uppercase">
          {item.status}
        </p>
      </div>
      {item.databaseValue || item.gatewayValue ? (
        <p className="mt-3 text-sm">
          Our records: {item.databaseValue ?? "none"} · Razorpay: {item.gatewayValue ?? "none"}
        </p>
      ) : null}
      {[item.gatewayPaymentId, item.gatewayRefundId, item.reference].some(Boolean) ? (
        <p className="mt-1 text-xs break-all text-brand-muted">
          {[item.gatewayPaymentId, item.gatewayRefundId, item.reference].filter(Boolean).join(" · ")}
        </p>
      ) : null}
      {resolved ? (
        <p className="mt-3 text-sm text-brand-muted">
          Resolved{item.resolvedByName ? ` by ${item.resolvedByName}` : ""}
          {item.resolvedAt ? ` on ${formatAdminWhen(item.resolvedAt)}` : ""}: {item.resolutionNote}
        </p>
      ) : (
        <Button variant="outlined" onClick={onResolve} sx={{ mt: 2, minHeight: 44, borderRadius: "14px" }}>
          Mark resolved
        </Button>
      )}
    </article>
  );
}

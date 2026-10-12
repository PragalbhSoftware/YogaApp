import { useMemo } from "react";
import { Link } from "react-router-dom";
import { toast } from "sonner";
import { PriceText } from "@/components/common/price-text";
import { adminBookingPath } from "@/constants/routes";
import { AdminTable, RowCard, type AdminColumn } from "@/features/admin/components/admin-table";
import { FilterChips } from "@/features/admin/components/filter-chips";
import { MarkPaidButton } from "@/features/admin/components/mark-paid-button";
import { canMarkPaid } from "@/features/admin/list-utils";
import { PagedList } from "@/features/admin/components/paged-list";
import { PayoutExportBar } from "@/features/admin/components/payout-export-bar";
import { StatusPill } from "@/features/admin/components/status-pill";
import { useAdminPayouts, useMarkPayoutPaid } from "@/features/admin/hooks/use-admin";
import { ADMIN_PAGE_SIZE, useListParams } from "@/features/admin/hooks/use-list-params";
import { payoutStatuses, type AdminPayout } from "@/features/admin/types";
import { formatAdminWhen } from "@/features/admin/utils";
import { payoutReasonLabel } from "@/features/bookings/utils/status";
import { toUserMessage } from "@/services/http/api-error";

const filters = payoutStatuses.map((status) => ({ value: status, label: status }));

export function PayoutsList() {
  const { searchParams, page, update, setPage } = useListParams();
  const status = payoutStatuses.find((value) => value === searchParams.get("payout")) ?? "Pending";
  const payouts = useAdminPayouts({ status, page, pageSize: ADMIN_PAGE_SIZE });
  const markPaid = useMarkPayoutPaid();

  function confirmPaid(id: string) {
    markPaid.mutate(id, {
      onSuccess: () => toast.success("Marked paid. This amount drops out of what you owe."),
      onError: (error) => toast.error(toUserMessage(error)),
    });
  }

  const busyId = markPaid.isPending ? markPaid.variables : undefined;

  return (
    <>
      <FilterChips value={status} options={filters} onChange={(next) => update({ payout: next ?? "Pending" })} label="Payout status" />
      <PayoutExportBar status={status} rowCount={payouts.data?.total ?? 0} />
      <PagedList
        query={payouts}
        noun="payouts"
        empty={{
          title: "No payouts in this list",
          description: "A payout is created when a session is completed, marked no-show, or cancelled late.",
        }}
        onPageChange={setPage}
      >
        {(items, stale) => <PayoutsTable payouts={items} stale={stale} busyId={busyId} onMarkPaid={confirmPaid} />}
      </PagedList>
    </>
  );
}

type PayoutsTableProps = {
  payouts: AdminPayout[];
  stale: boolean;
  busyId: string | undefined;
  onMarkPaid: (id: string) => void;
};

function PayoutsTable({ payouts, stale, busyId, onMarkPaid }: PayoutsTableProps) {
  const columns = useMemo<AdminColumn<AdminPayout>[]>(
    () => [
      {
        id: "instructor",
        header: "Instructor",
        cell: ({ row }) => (
          <Link
            className="font-medium text-brand-text hover:text-brand-primary hover:underline"
            to={adminBookingPath(row.original.bookingId)}
          >
            {row.original.providerName}
          </Link>
        ),
      },
      {
        id: "reason",
        header: "Reason",
        cell: ({ row }) => (
          <span className="flex flex-col">
            <span>{payoutReasonLabel(row.original.bookingStatus)}</span>
            <span className="text-xs whitespace-nowrap text-brand-muted">{formatAdminWhen(row.original.createdAt)}</span>
          </span>
        ),
      },
      {
        id: "gross",
        header: "Gross",
        meta: { className: "text-right" },
        cell: ({ row }) => <PriceText amount={row.original.grossAmount} className="min-w-0 text-sm font-normal" />,
      },
      {
        id: "fee",
        header: "Your take",
        meta: { className: "text-right" },
        cell: ({ row }) => (
          <span className="whitespace-nowrap">
            <PriceText amount={row.original.feeAmount} className="min-w-0 text-sm font-normal" />
            <span className="ml-1 text-xs text-brand-muted">{row.original.feePercent}%</span>
          </span>
        ),
      },
      {
        id: "net",
        header: "Pay instructor",
        meta: { className: "text-right" },
        cell: ({ row }) => <PriceText amount={row.original.netAmount} className="min-w-0 text-sm" />,
      },
      { id: "status", header: "Status", cell: ({ row }) => <StatusPill status={row.original.status} /> },
      {
        id: "action",
        header: () => <span className="sr-only">Action</span>,
        meta: { className: "text-right" },
        cell: ({ row }) =>
          canMarkPaid(row.original) ? (
            <MarkPaidButton payout={row.original} busy={busyId === row.original.id} onConfirm={onMarkPaid} />
          ) : null,
      },
    ],
    [busyId, onMarkPaid],
  );

  return (
    <AdminTable
      caption="Payouts"
      data={payouts}
      columns={columns}
      getRowId={(payout) => payout.id}
      stale={stale}
      renderCard={(payout) => (
        <RowCard
          title={
            <Link className="hover:text-brand-primary" to={adminBookingPath(payout.bookingId)}>
              {payout.providerName}
            </Link>
          }
          status={<StatusPill status={payout.status} />}
          action={
            canMarkPaid(payout) ? (
              <MarkPaidButton payout={payout} busy={busyId === payout.id} onConfirm={onMarkPaid} />
            ) : null
          }
        >
          <p>
            {payoutReasonLabel(payout.bookingStatus)} · {formatAdminWhen(payout.createdAt)}
          </p>
          <p className="text-brand-text">
            Pay <PriceText amount={payout.netAmount} className="min-w-0 text-sm" /> · you keep{" "}
            <PriceText amount={payout.feeAmount} className="min-w-0 text-sm font-normal" />
          </p>
        </RowCard>
      )}
    />
  );
}

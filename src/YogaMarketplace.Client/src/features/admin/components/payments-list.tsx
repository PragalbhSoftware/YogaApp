import { useMemo } from "react";
import { Link } from "react-router-dom";
import { PhoneText } from "@/components/common/phone-text";
import { PriceText } from "@/components/common/price-text";
import { adminBookingPath } from "@/constants/routes";
import { AdminTable, RowCard, type AdminColumn } from "@/features/admin/components/admin-table";
import { FilterChips } from "@/features/admin/components/filter-chips";
import { PagedList } from "@/features/admin/components/paged-list";
import { StatusPill } from "@/features/admin/components/status-pill";
import { useAdminPayments } from "@/features/admin/hooks/use-admin";
import { ADMIN_PAGE_SIZE, useListParams } from "@/features/admin/hooks/use-list-params";
import { paymentStatuses, type AdminPayment } from "@/features/admin/types";
import { displayName, formatAdminWhen } from "@/features/admin/utils";
import { paymentStatusLabel } from "@/features/bookings/utils/status";

const filters = [
  { value: null, label: "All" },
  ...paymentStatuses.map((status) => ({ value: status, label: paymentStatusLabel(status) })),
];

export function PaymentsList() {
  const { searchParams, page, update, setPage } = useListParams();
  const status = paymentStatuses.find((value) => value === searchParams.get("status"));
  const payments = useAdminPayments({ status, page, pageSize: ADMIN_PAGE_SIZE });

  return (
    <>
      <FilterChips value={status ?? null} options={filters} onChange={(next) => update({ status: next })} label="Payment status" />
      <PagedList
        query={payments}
        noun="payments"
        empty={{ title: "No payments in this list", description: "Paid bookings appear here after checkout." }}
        onPageChange={setPage}
      >
        {(items, stale) => <PaymentsTable payments={items} stale={stale} />}
      </PagedList>
    </>
  );
}

function PaymentsTable({ payments, stale }: { payments: AdminPayment[]; stale: boolean }) {
  const columns = useMemo<AdminColumn<AdminPayment>[]>(
    () => [
      {
        id: "when",
        header: "Paid on",
        cell: ({ row }) => (
          <Link
            className="font-medium whitespace-nowrap text-brand-text hover:text-brand-primary hover:underline"
            to={adminBookingPath(row.original.bookingId)}
          >
            {formatAdminWhen(row.original.createdAt)}
          </Link>
        ),
      },
      { id: "instructor", header: "Instructor", cell: ({ row }) => row.original.providerName },
      {
        id: "customer",
        header: "Customer",
        cell: ({ row }) => (
          <span className="flex flex-col">
            <span>{displayName(row.original.customerName, row.original.customerPhone)}</span>
            <PhoneText value={row.original.customerPhone} className="text-xs text-brand-muted" />
          </span>
        ),
      },
      {
        id: "status",
        header: "Status",
        cell: ({ row }) => <StatusPill status={row.original.status} label={paymentStatusLabel(row.original.status)} />,
      },
      {
        id: "reference",
        header: "Razorpay id",
        meta: { className: "text-xs text-brand-muted" },
        cell: ({ row }) => row.original.gatewayPaymentId ?? "—",
      },
      {
        id: "amount",
        header: "Amount",
        meta: { className: "text-right" },
        cell: ({ row }) => <PaymentAmount payment={row.original} />,
      },
    ],
    [],
  );

  return (
    <AdminTable
      caption="Payments"
      data={payments}
      columns={columns}
      getRowId={(payment) => payment.id}
      stale={stale}
      renderCard={(payment) => (
        <RowCard
          title={
            <Link className="hover:text-brand-primary" to={adminBookingPath(payment.bookingId)}>
              {payment.providerName}
            </Link>
          }
          status={<StatusPill status={payment.status} label={paymentStatusLabel(payment.status)} />}
        >
          <p>
            {displayName(payment.customerName, payment.customerPhone)} · <PhoneText value={payment.customerPhone} />
          </p>
          <p>{formatAdminWhen(payment.createdAt)}</p>
          <p className="text-brand-text">
            <PaymentAmount payment={payment} />
          </p>
        </RowCard>
      )}
    />
  );
}

function PaymentAmount({ payment }: { payment: AdminPayment }) {
  return (
    <span className="inline-flex flex-col items-end">
      <PriceText amount={payment.amount} className="min-w-0 text-sm" />
      {payment.refundedAmount > 0 ? (
        <span className="text-xs whitespace-nowrap text-brand-muted">
          Refunded <PriceText amount={payment.refundedAmount} className="min-w-0 text-xs font-normal" />
        </span>
      ) : null}
    </span>
  );
}

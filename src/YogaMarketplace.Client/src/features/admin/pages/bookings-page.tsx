import { useMemo } from "react";
import { Link } from "react-router-dom";
import { Button, TextField } from "@mui/material";
import { PhoneText } from "@/components/common/phone-text";
import { PriceText } from "@/components/common/price-text";
import { adminBookingPath } from "@/constants/routes";
import { AdminPage } from "@/features/admin/components/admin-page";
import { AdminTable, RowCard, type AdminColumn } from "@/features/admin/components/admin-table";
import { FilterChips } from "@/features/admin/components/filter-chips";
import { PagedList } from "@/features/admin/components/paged-list";
import { StatusPill } from "@/features/admin/components/status-pill";
import { useAdminBookings } from "@/features/admin/hooks/use-admin";
import { ADMIN_PAGE_SIZE, useListParams } from "@/features/admin/hooks/use-list-params";
import type { AdminBooking } from "@/features/admin/types";
import { displayName, formatSessionWhen } from "@/features/admin/utils";
import { bookingStatuses, isBookingStatus } from "@/features/bookings/types";
import { statusFilterLabel } from "@/features/bookings/utils/status";

const statusFilters = [
  { value: null, label: "All" },
  ...bookingStatuses.map((status) => ({ value: status, label: statusFilterLabel(status) })),
];

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

function readDate(value: string | null) {
  return value && ISO_DATE.test(value) ? value : undefined;
}

export function BookingsPage() {
  const { searchParams, page, update, setPage } = useListParams();
  const statusParam = searchParams.get("status");
  const status = isBookingStatus(statusParam) ? statusParam : undefined;
  const from = readDate(searchParams.get("from"));
  const to = readDate(searchParams.get("to"));
  const bookings = useAdminBookings({ status, from, to, page, pageSize: ADMIN_PAGE_SIZE });

  return (
    <AdminPage
      kicker="Sessions"
      title="Bookings"
      lead="Every booking on the marketplace, newest first. Open one for payment, payout and cancel."
    >
      <section className="flex flex-col gap-4 rounded-[24px] border border-brand-border bg-brand-surface p-4 sm:p-5">
        <FilterChips
          value={status ?? null}
          options={statusFilters}
          onChange={(next) => update({ status: next })}
          label="Booking status"
        />
        <DateRangeForm
          key={`${from ?? ""}|${to ?? ""}`}
          from={from}
          to={to}
          onApply={(range) => update({ from: range.from ?? null, to: range.to ?? null })}
        />
      </section>

      <PagedList
        query={bookings}
        noun="bookings"
        empty={{ title: "No bookings in this list", description: "Try another status or date range." }}
        onPageChange={setPage}
      >
        {(items, stale) => <BookingsTable bookings={items} stale={stale} />}
      </PagedList>
    </AdminPage>
  );
}

type DateRange = { from?: string; to?: string };

function DateRangeForm({ from, to, onApply }: DateRange & { onApply: (range: DateRange) => void }) {
  return (
    <form
      aria-label="Session dates"
      className="grid gap-3 sm:grid-cols-[1fr_1fr_auto_auto]"
      onSubmit={(event) => {
        event.preventDefault();
        const form = new FormData(event.currentTarget);
        onApply({
          from: readDate(String(form.get("from") ?? "")),
          to: readDate(String(form.get("to") ?? "")),
        });
      }}
    >
      <TextField
        label="Session from"
        name="from"
        type="date"
        size="small"
        defaultValue={from ?? ""}
        slotProps={{ inputLabel: { shrink: true } }}
      />
      <TextField
        label="Session to"
        name="to"
        type="date"
        size="small"
        defaultValue={to ?? ""}
        slotProps={{ inputLabel: { shrink: true } }}
      />
      <Button type="submit" variant="contained" sx={{ minHeight: 40, borderRadius: "12px" }}>
        Apply
      </Button>
      {from || to ? (
        <Button type="button" variant="text" onClick={() => onApply({})} sx={{ minHeight: 40 }}>
          Clear dates
        </Button>
      ) : null}
    </form>
  );
}

function BookingsTable({ bookings, stale }: { bookings: AdminBooking[]; stale: boolean }) {
  const columns = useMemo<AdminColumn<AdminBooking>[]>(
    () => [
      {
        id: "session",
        header: "Session",
        cell: ({ row }) => (
          <Link
            className="font-medium whitespace-nowrap text-brand-text hover:text-brand-primary hover:underline"
            to={adminBookingPath(row.original.id)}
          >
            {formatSessionWhen(row.original.date, row.original.start, row.original.end)}
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
      { id: "mode", header: "Mode", cell: ({ row }) => row.original.mode },
      {
        id: "status",
        header: "Status",
        cell: ({ row }) => <StatusPill status={row.original.status} label={statusFilterLabel(row.original.status)} />,
      },
      {
        id: "amount",
        header: "Amount",
        meta: { className: "text-right" },
        cell: ({ row }) => <BookingAmount booking={row.original} />,
      },
    ],
    [],
  );

  return (
    <AdminTable
      caption="Bookings"
      data={bookings}
      columns={columns}
      getRowId={(booking) => booking.id}
      stale={stale}
      renderCard={(booking) => (
        <RowCard
          title={
            <Link className="hover:text-brand-primary" to={adminBookingPath(booking.id)}>
              {booking.providerName}
            </Link>
          }
          status={<StatusPill status={booking.status} label={statusFilterLabel(booking.status)} />}
        >
          <p>{formatSessionWhen(booking.date, booking.start, booking.end)}</p>
          <p>
            {displayName(booking.customerName, booking.customerPhone)} · <PhoneText value={booking.customerPhone} />
          </p>
          <p className="text-brand-text">
            {booking.mode} · <BookingAmount booking={booking} />
          </p>
        </RowCard>
      )}
    />
  );
}

function BookingAmount({ booking }: { booking: AdminBooking }) {
  if (booking.paidWithCredit) return <span className="whitespace-nowrap">1 credit</span>;
  return <PriceText amount={booking.amount} className="min-w-0 text-sm" />;
}

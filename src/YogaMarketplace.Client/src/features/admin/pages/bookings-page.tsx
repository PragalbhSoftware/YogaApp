import { Link, useSearchParams } from "react-router-dom";
import { Button, TextField } from "@mui/material";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { PhoneText } from "@/components/common/phone-text";
import { PriceText } from "@/components/common/price-text";
import { adminBookingPath } from "@/constants/routes";
import { AdminPage } from "@/features/admin/components/admin-page";
import { FilterChips } from "@/features/admin/components/filter-chips";
import { useAdminBookings } from "@/features/admin/hooks/use-admin";
import { displayName, formatSessionWhen, listCap } from "@/features/admin/utils";
import { bookingStatuses, isBookingStatus } from "@/features/bookings/types";
import { statusFilterLabel } from "@/features/bookings/utils/status";
import { toUserMessage } from "@/services/http/api-error";

const statusFilters = [
  { value: null, label: "All" },
  ...bookingStatuses.map((status) => ({ value: status, label: statusFilterLabel(status) })),
];

export function BookingsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const statusParam = searchParams.get("status");
  const status = isBookingStatus(statusParam) ? statusParam : undefined;
  const from = searchParams.get("from") || undefined;
  const to = searchParams.get("to") || undefined;
  const bookings = useAdminBookings({ status, from, to });

  function setParam(key: string, value: string | null) {
    const params = new URLSearchParams(searchParams);
    if (value) params.set(key, value);
    else params.delete(key);
    setSearchParams(params, { replace: true });
  }

  return (
    <AdminPage
      kicker="Sessions"
      title="Bookings"
      lead="Read-only list of marketplace bookings. Open a row for payment and payout detail."
      note={listCap}
    >
      <FilterChips
        value={status ?? null}
        options={statusFilters}
        onChange={(next) => setParam("status", next)}
        label="Booking status"
      />

      <form
        className="grid gap-3 sm:grid-cols-[1fr_1fr_auto]"
        onSubmit={(event) => {
          event.preventDefault();
          const form = new FormData(event.currentTarget);
          const params = new URLSearchParams(searchParams);
          const fromValue = String(form.get("from") ?? "").trim();
          const toValue = String(form.get("to") ?? "").trim();
          if (fromValue) params.set("from", fromValue);
          else params.delete("from");
          if (toValue) params.set("to", toValue);
          else params.delete("to");
          setSearchParams(params, { replace: true });
        }}
      >
        <TextField
          label="From"
          name="from"
          type="date"
          defaultValue={from ?? ""}
          slotProps={{ inputLabel: { shrink: true } }}
        />
        <TextField
          label="To"
          name="to"
          type="date"
          defaultValue={to ?? ""}
          slotProps={{ inputLabel: { shrink: true } }}
        />
        <Button type="submit" variant="contained" sx={{ minHeight: 56, borderRadius: "14px" }}>
          Apply
        </Button>
      </form>

      {bookings.isLoading ? <BookingListSkeleton /> : null}
      {bookings.isError ? (
        <ErrorState message={toUserMessage(bookings.error)} onRetry={() => void bookings.refetch()} />
      ) : null}
      {bookings.isSuccess && bookings.data.length === 0 ? (
        <EmptyState title="No bookings in this list" description="Try another status or date range." />
      ) : null}
      {bookings.isSuccess && bookings.data.length > 0 ? (
        <div className="space-y-3">
          {bookings.data.map((booking) => (
            <article
              key={booking.id}
              className="flex flex-wrap items-center justify-between gap-3 rounded-[24px] border border-brand-border bg-brand-surface p-5"
            >
              <div className="min-w-0">
                <h2 className="font-heading text-lg font-medium">{booking.providerName}</h2>
                <p className="mt-1 text-sm text-brand-muted">{formatSessionWhen(booking.date, booking.start, booking.end)}</p>
                <p className="mt-1 text-sm text-brand-muted">
                  {displayName(booking.customerName, booking.customerPhone)} · <PhoneText value={booking.customerPhone} />
                </p>
                <p className="mt-1 text-sm">
                  {booking.mode} · {statusFilterLabel(booking.status)}
                  {booking.paymentStatus ? ` · ${booking.paymentStatus}` : ""} ·{" "}
                  <PriceText amount={booking.amount} className="min-w-0 text-sm" />
                </p>
              </div>
              <Button
                component={Link}
                to={adminBookingPath(booking.id)}
                variant="outlined"
                sx={{ minHeight: 44, borderRadius: "14px" }}
              >
                View
              </Button>
            </article>
          ))}
        </div>
      ) : null}
    </AdminPage>
  );
}

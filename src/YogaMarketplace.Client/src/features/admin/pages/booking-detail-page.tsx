import { useState, type ReactNode } from "react";
import { Link, useParams } from "react-router-dom";
import { Button } from "@mui/material";
import { ArrowLeft, Ban } from "lucide-react";
import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { PageLoader } from "@/components/common/page-loader";
import { PhoneText } from "@/components/common/phone-text";
import { PriceText } from "@/components/common/price-text";
import { adminUserPath, routes } from "@/constants/routes";
import { AdminCancelDialog } from "@/features/admin/components/admin-cancel-dialog";
import { AdminPage } from "@/features/admin/components/admin-page";
import { useAdminBooking, useAdminCancelBooking } from "@/features/admin/hooks/use-admin";
import type { AdminBooking } from "@/features/admin/types";
import { displayName, formatAdminWhen, formatSessionWhen, httpHref } from "@/features/admin/utils";
import { paymentStatusLabel, statusFilterLabel } from "@/features/bookings/utils/status";
import { toUserMessage } from "@/services/http/api-error";
import { formatInr } from "@/utils/money";

const PAGE_LEAD = "Customers cancel or reschedule from their own bookings. Cancel here only when they can’t.";
const CANCELLABLE_STATUSES = ["PendingAccept", "Upcoming"];

export function BookingDetailPage() {
  const { id } = useParams();
  const bookingQuery = useAdminBooking(id);

  if (bookingQuery.isLoading) return <PageLoader />;
  if (bookingQuery.isError) {
    return (
      <AdminPage kicker="Sessions" title="Booking" lead={PAGE_LEAD}>
        <ErrorState message={toUserMessage(bookingQuery.error)} onRetry={() => void bookingQuery.refetch()} />
      </AdminPage>
    );
  }
  if (!bookingQuery.data) {
    return (
      <AdminPage kicker="Sessions" title="Booking" lead={PAGE_LEAD}>
        <EmptyState title="Booking not found" description="It may have been removed." />
      </AdminPage>
    );
  }

  return <BookingDetail booking={bookingQuery.data} />;
}

function BookingDetail({ booking }: { booking: AdminBooking }) {
  const [cancelOpen, setCancelOpen] = useState(false);
  const cancelBooking = useAdminCancelBooking();
  const meet = httpHref(booking.meetLink);
  const canCancel = CANCELLABLE_STATUSES.includes(booking.status) && booking.paymentStatus === "Paid";

  const confirmCancel = (reason: string) => {
    cancelBooking.mutate(
      { id: booking.id, reason },
      {
        onSuccess: (updated) => {
          setCancelOpen(false);
          toast.success(`Cancelled. ${formatInr(updated.refundedAmount ?? updated.amount)} refunded to the customer.`);
        },
        onError: (error) => toast.error(toUserMessage(error)),
      },
    );
  };

  return (
    <AdminPage kicker="Sessions" title={booking.serviceTitle} lead={PAGE_LEAD}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Button
          component={Link}
          to={routes.adminBookings}
          variant="text"
          startIcon={<ArrowLeft className="size-4" />}
          sx={{ ml: -1 }}
        >
          Back to bookings
        </Button>
        {canCancel ? (
          <Button
            variant="outlined"
            color="warning"
            startIcon={<Ban className="size-4" />}
            onClick={() => setCancelOpen(true)}
            sx={{ minHeight: 44, borderRadius: "14px" }}
          >
            Cancel booking
          </Button>
        ) : null}
      </div>

      <dl className="grid gap-4 rounded-[24px] border border-brand-border bg-brand-surface p-5 text-sm sm:grid-cols-2">
        <Fact label="Instructor">{booking.providerName}</Fact>
        <Fact label="Customer">
          <Link className="font-medium text-brand-primary" to={adminUserPath(booking.customerId)}>
            {displayName(booking.customerName, booking.customerPhone)}
          </Link>
          {" · "}
          <PhoneText value={booking.customerPhone} />
        </Fact>
        <Fact label="When">{formatSessionWhen(booking.date, booking.start, booking.end)}</Fact>
        <Fact label="Mode">{booking.mode}</Fact>
        <Fact label="Status">{statusFilterLabel(booking.status)}</Fact>
        <Fact label="Amount">
          <PriceText amount={booking.amount} className="min-w-0 text-sm" /> · {booking.currency}
        </Fact>
        {booking.paymentStatus ? <Fact label="Payment">{paymentStatusLabel(booking.paymentStatus)}</Fact> : null}
        {booking.refundedAmount ? (
          <Fact label="Refunded">
            <PriceText amount={booking.refundedAmount} className="min-w-0 text-sm" />
          </Fact>
        ) : null}
        {booking.lateCancelFee ? (
          <Fact label="Late-cancel fee kept">
            <PriceText amount={booking.lateCancelFee} className="min-w-0 text-sm" />
          </Fact>
        ) : null}
        {booking.cancelledBy ? (
          <Fact label="Cancelled by">{booking.cancelledBy === "Admin" ? "Yoga Marketplace" : "Customer"}</Fact>
        ) : null}
        {booking.cancelReason ? <Fact label="Cancel reason">{booking.cancelReason}</Fact> : null}
        {booking.gatewayOrderId ? <Fact label="Order">{booking.gatewayOrderId}</Fact> : null}
        {booking.gatewayPaymentId ? <Fact label="Payment id">{booking.gatewayPaymentId}</Fact> : null}
        {booking.payoutStatus ? (
          <Fact label="Payout">
            {booking.payoutStatus}
            {booking.payoutNet != null ? (
              <>
                {" "}
                · <PriceText amount={booking.payoutNet} className="min-w-0 text-sm" />
              </>
            ) : null}
          </Fact>
        ) : null}
        {booking.reviewRating ? <Fact label="Review">{booking.reviewRating} / 5</Fact> : null}
        {booking.homeAddress ? <Fact label="Home address">{booking.homeAddress}</Fact> : null}
        {booking.landmark ? <Fact label="Landmark">{booking.landmark}</Fact> : null}
        {booking.studioAddress ? <Fact label="Studio">{booking.studioAddress}</Fact> : null}
        {meet ? (
          <Fact label="Meet">
            <a className="font-medium text-brand-primary" href={meet} target="_blank" rel="noreferrer">
              Open link
            </a>
          </Fact>
        ) : null}
        <Fact label="Created">{formatAdminWhen(booking.createdAt)}</Fact>
      </dl>

      <AdminCancelDialog
        booking={booking}
        open={cancelOpen}
        busy={cancelBooking.isPending}
        onClose={() => setCancelOpen(false)}
        onConfirm={confirmCancel}
      />
    </AdminPage>
  );
}

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt className="text-brand-muted">{label}</dt>
      <dd className="mt-0.5">{children}</dd>
    </div>
  );
}

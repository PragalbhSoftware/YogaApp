import { useState } from "react";
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
import { Fact, FactCard } from "@/features/admin/components/fact-card";
import { StatusPill } from "@/features/admin/components/status-pill";
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
  const canCancel = CANCELLABLE_STATUSES.includes(booking.status) && booking.paymentStatus === "Paid";

  const confirmCancel = (reason: string) => {
    cancelBooking.mutate(
      { id: booking.id, reason },
      {
        onSuccess: (updated) => {
          setCancelOpen(false);
          toast.success(
            updated.paidWithCredit
              ? "Cancelled. The credit went back to the customer."
              : `Cancelled. ${formatInr(updated.refundedAmount ?? updated.amount)} refunded to the customer.`,
          );
        },
        onError: (error) => toast.error(toUserMessage(error)),
      },
    );
  };

  return (
    <AdminPage kicker="Sessions" title={booking.serviceTitle} lead={PAGE_LEAD}>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-3">
          <Button
            component={Link}
            to={routes.adminBookings}
            variant="text"
            startIcon={<ArrowLeft className="size-4" />}
            sx={{ ml: -1 }}
          >
            Back to bookings
          </Button>
          <StatusPill status={booking.status} label={statusFilterLabel(booking.status)} />
        </div>
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

      <div className="grid gap-6 lg:grid-cols-2 lg:items-start">
        <SessionFacts booking={booking} />
        <MoneyFacts booking={booking} />
      </div>

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

function SessionFacts({ booking }: { booking: AdminBooking }) {
  const meet = httpHref(booking.meetLink);
  return (
    <FactCard title="Session">
      <Fact label="When" wide>
        {formatSessionWhen(booking.date, booking.start, booking.end)}
      </Fact>
      <Fact label="Instructor">{booking.providerName}</Fact>
      <Fact label="Mode">{booking.mode}</Fact>
      <Fact label="Customer" wide>
        <Link className="font-medium text-brand-primary hover:underline" to={adminUserPath(booking.customerId)}>
          {displayName(booking.customerName, booking.customerPhone)}
        </Link>
        {" · "}
        <PhoneText value={booking.customerPhone} />
      </Fact>
      {booking.homeAddress ? (
        <Fact label="Home address" wide>
          {booking.homeAddress}
        </Fact>
      ) : null}
      {booking.landmark ? <Fact label="Landmark">{booking.landmark}</Fact> : null}
      {booking.studioAddress ? (
        <Fact label="Studio" wide>
          {booking.studioAddress}
        </Fact>
      ) : null}
      {meet ? (
        <Fact label="Meet">
          <a className="font-medium text-brand-primary" href={meet} target="_blank" rel="noreferrer">
            Open link
          </a>
        </Fact>
      ) : null}
      {booking.cancelledBy ? (
        <Fact label="Cancelled by">{booking.cancelledBy === "Admin" ? "Yoga Marketplace" : "Customer"}</Fact>
      ) : null}
      {booking.cancelReason ? (
        <Fact label="Cancel reason" wide>
          {booking.cancelReason}
        </Fact>
      ) : null}
      {booking.reviewRating ? <Fact label="Review">{booking.reviewRating} / 5</Fact> : null}
      <Fact label="Booked">{formatAdminWhen(booking.createdAt)}</Fact>
    </FactCard>
  );
}

function MoneyFacts({ booking }: { booking: AdminBooking }) {
  return (
    <FactCard title="Payment and payout">
      <Fact label={booking.paidWithCredit ? "Instructor gross (frozen)" : "Amount"}>
        <PriceText amount={booking.amount} className="min-w-0 text-sm" /> · {booking.currency}
      </Fact>
      {booking.paidWithCredit ? (
        <Fact label="Paid with">
          1 credit
          {booking.creditValue != null ? (
            <>
              {" · worth "}
              <PriceText amount={booking.creditValue} className="min-w-0 text-sm" />
            </>
          ) : null}
        </Fact>
      ) : null}
      {booking.paymentStatus ? (
        <Fact label="Payment">
          <StatusPill status={booking.paymentStatus} label={paymentStatusLabel(booking.paymentStatus)} />
        </Fact>
      ) : null}
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
      {booking.payoutStatus ? (
        <Fact label="Payout">
          <span className="inline-flex flex-wrap items-center gap-2">
            <StatusPill status={booking.payoutStatus} />
            {booking.payoutNet != null ? <PriceText amount={booking.payoutNet} className="min-w-0 text-sm" /> : null}
          </span>
        </Fact>
      ) : null}
      {booking.gatewayOrderId ? (
        <Fact label="Razorpay order" wide>
          <span className="text-xs">{booking.gatewayOrderId}</span>
        </Fact>
      ) : null}
      {booking.gatewayPaymentId ? (
        <Fact label="Razorpay payment" wide>
          <span className="text-xs">{booking.gatewayPaymentId}</span>
        </Fact>
      ) : null}
    </FactCard>
  );
}

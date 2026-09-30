import { Link } from "react-router-dom";
import { Skeleton } from "@mui/material";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { PhoneText } from "@/components/common/phone-text";
import { adminBookingPath, routes } from "@/constants/routes";
import { AdminPage } from "@/features/admin/components/admin-page";
import { QueueCard } from "@/features/admin/components/queue-card";
import { StatCard } from "@/features/admin/components/stat-card";
import { useOwnerHome } from "@/features/admin/hooks/use-owner-home";
import { displayName, formatSessionWhen } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";
import { formatInr } from "@/utils/money";

export function DashboardPage() {
  const home = useOwnerHome();

  return (
    <AdminPage
      kicker="Owner"
      title="Home"
      lead="What you earned, what you owe instructors, and work waiting today."
    >
      {home.isLoading ? <DashboardSkeleton /> : null}
      {home.error ? (
        <ErrorState message={toUserMessage(home.error)} onRetry={home.refetch} />
      ) : null}
      {!home.isLoading && !home.error ? (
        <div className="space-y-8">
          <section className="grid gap-3 sm:grid-cols-2">
            <StatCard
              label="Your fee"
              value={formatInr(home.yourFee)}
              hint="Kept on completed, no-show and late-cancel payouts"
            />
            <StatCard
              label="You owe instructors"
              value={formatInr(home.oweInstructors)}
              hint="Pay this from Razorpay settlements"
            />
            <StatCard
              label="Paid GMV"
              value={formatInr(home.gmvPaid)}
              hint="Customer spend, not profit"
            />
            <StatCard
              label="Take rate"
              value={home.takeRate == null ? "—" : `${home.takeRate}%`}
              hint="On each instructor payout"
            />
          </section>

          <section className="space-y-3">
            <h2 className="font-heading text-lg font-medium">Needs you</h2>
            <div className="grid gap-3 sm:grid-cols-2">
              <QueueCard
                to={routes.adminApprovals}
                label="Pending instructors"
                value={home.pendingInstructorCount}
                hint="Verify so customers can browse them"
              />
              <QueueCard
                to={`${routes.adminBookings}?status=PendingAccept`}
                label="Waiting for accept"
                value={home.pendingAcceptCount}
                hint="Paid sessions the instructor has not accepted"
              />
              <QueueCard
                to={`${routes.adminBookings}?status=Upcoming`}
                label="Upcoming sessions"
                value={home.upcomingCount}
                hint="Accepted and still ahead"
              />
              <QueueCard
                to={`${routes.adminTransactions}?tab=payouts`}
                label="Payouts to send"
                value={home.payoutsToSend}
                hint="Net amount still pending"
              />
            </div>
          </section>

          <TodaySessions home={home} />
        </div>
      ) : null}
    </AdminPage>
  );
}

function TodaySessions({ home }: { home: ReturnType<typeof useOwnerHome> }) {
  const sessions = home.todaySessions;

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-end justify-between gap-2">
        <h2 className="font-heading text-lg font-medium">Today</h2>
        <Link className="text-sm font-medium text-brand-primary" to={`${routes.adminBookings}?status=Upcoming`}>
          All upcoming
        </Link>
      </div>
      {sessions.isLoading ? <BookingListSkeleton /> : null}
      {sessions.isError ? (
        <ErrorState message={toUserMessage(sessions.error)} onRetry={() => void sessions.refetch()} />
      ) : null}
      {sessions.isSuccess && sessions.data.length === 0 ? (
        <EmptyState
          title="No upcoming sessions today"
          description="Accepted sessions for today will show here."
        />
      ) : null}
      {sessions.isSuccess && sessions.data.length > 0 ? (
        <ul className="space-y-3">
          {[...sessions.data]
            .sort((a, b) => a.start.localeCompare(b.start))
            .map((booking) => (
              <li key={booking.id}>
                <Link
                  to={adminBookingPath(booking.id)}
                  className="block rounded-[24px] border border-brand-border bg-brand-surface px-5 py-4"
                >
                  <p className="font-heading text-lg font-medium">{booking.providerName}</p>
                  <p className="mt-1 text-sm text-brand-muted">
                    {formatSessionWhen(booking.date, booking.start, booking.end)}
                  </p>
                  <p className="mt-1 text-sm text-brand-muted">
                    {displayName(booking.customerName, booking.customerPhone)}
                    {" · "}
                    <PhoneText value={booking.customerPhone} />
                    {" · "}
                    {booking.mode}
                    {" · "}
                    {formatInr(booking.amount)}
                  </p>
                </Link>
              </li>
            ))}
        </ul>
      ) : null}
    </section>
  );
}

function DashboardSkeleton() {
  return (
    <div className="space-y-6" aria-hidden="true">
      <div className="grid gap-3 sm:grid-cols-2">
        {Array.from({ length: 4 }, (_, index) => (
          <Skeleton key={index} variant="rounded" height={112} sx={{ borderRadius: "24px" }} />
        ))}
      </div>
      <BookingListSkeleton />
    </div>
  );
}

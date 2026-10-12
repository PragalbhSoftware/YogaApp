import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { Skeleton } from "@mui/material";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { PhoneText } from "@/components/common/phone-text";
import { adminBookingPath, routes } from "@/constants/routes";
import { AdminPage } from "@/features/admin/components/admin-page";
import { TableSkeleton } from "@/features/admin/components/paged-list";
import { QueueCard } from "@/features/admin/components/queue-card";
import { StatCard } from "@/features/admin/components/stat-card";
import { useOwnerHome } from "@/features/admin/hooks/use-owner-home";
import { displayName } from "@/features/admin/utils";
import { toUserMessage } from "@/services/http/api-error";
import { formatTimeRange12 } from "@/utils/clock";
import { formatInr } from "@/utils/money";

type OwnerHome = ReturnType<typeof useOwnerHome>;

export function DashboardPage() {
  const home = useOwnerHome();

  return (
    <AdminPage kicker="Owner" title="Home" lead="What you earned, what you owe instructors, and work waiting today.">
      {home.isLoading ? <DashboardSkeleton /> : null}
      {home.error ? <ErrorState message={toUserMessage(home.error)} onRetry={home.refetch} /> : null}
      {!home.isLoading && !home.error ? (
        <div className="flex flex-col gap-6">
          <MoneyStats home={home} />
          <div className="grid gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
            <NeedsYou home={home} />
            <TodaySessions home={home} />
          </div>
        </div>
      ) : null}
    </AdminPage>
  );
}

function MoneyStats({ home }: { home: OwnerHome }) {
  return (
    <section aria-label="Money" className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
      <StatCard
        label="Revenue"
        value={formatInr(home.revenue)}
        hint={`Bookings ${formatInr(home.gmvPaid)} + packs ${formatInr(home.packSales)}, net of refunds. Customer spend, not profit.`}
      />
      <StatCard label="Your fee" value={formatInr(home.yourFee)} hint="Kept on completed, no-show and late-cancel payouts" />
      <StatCard label="You owe instructors" value={formatInr(home.oweInstructors)} hint="Pay this from Razorpay settlements" />
      <StatCard label="Paid bookings" value={String(home.paidBookings)} hint="Paid for with money at booking" />
      <StatCard
        label="Credit bookings"
        value={String(home.creditBookings)}
        hint="Booked with a pack credit; the pack sale is the revenue"
      />
      <StatCard
        label="Take rate"
        value={home.takeRate == null ? "—" : `${home.takeRate}%`}
        hint="On each instructor payout"
      />
    </section>
  );
}

function Panel({ title, action, children }: { title: string; action?: ReactNode; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-3 rounded-[24px] border border-brand-border bg-brand-surface p-4 shadow-[0_8px_24px_rgba(37,49,39,0.04)] sm:p-5">
      <div className="flex flex-wrap items-center justify-between gap-2 px-1">
        <h2 className="font-heading text-lg font-medium">{title}</h2>
        {action}
      </div>
      {children}
    </section>
  );
}

function NeedsYou({ home }: { home: OwnerHome }) {
  return (
    <Panel title="Needs you">
      <div className="-mx-1 flex flex-col">
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
          hint="Sessions the instructor has not accepted"
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
    </Panel>
  );
}

function TodaySessions({ home }: { home: OwnerHome }) {
  const sessions = home.todaySessions;
  const todayLink = `${routes.adminBookings}?status=Upcoming&from=${home.today}&to=${home.today}`;

  return (
    <Panel
      title="Today"
      action={
        <Link className="text-sm font-medium text-brand-primary hover:underline" to={todayLink}>
          Open in bookings
        </Link>
      }
    >
      {sessions.isLoading ? <TableSkeleton /> : null}
      {sessions.isError ? (
        <ErrorState message={toUserMessage(sessions.error)} onRetry={() => void sessions.refetch()} />
      ) : null}
      {sessions.isSuccess && sessions.data.items.length === 0 ? (
        <EmptyState title="No upcoming sessions today" description="Accepted sessions for today will show here." />
      ) : null}
      {sessions.isSuccess && sessions.data.items.length > 0 ? (
        <ul className="-mx-1 flex flex-col">
          {[...sessions.data.items]
            .sort((a, b) => a.start.localeCompare(b.start))
            .map((booking) => (
              <li key={booking.id}>
                <Link
                  to={adminBookingPath(booking.id)}
                  className="flex items-start gap-4 rounded-2xl px-4 py-3 transition-colors hover:bg-brand-background"
                >
                  <span className="w-28 shrink-0 pt-0.5 text-sm font-medium whitespace-nowrap tabular-nums">
                    {formatTimeRange12(booking.start, booking.end)}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block font-medium">{booking.providerName}</span>
                    <span className="block text-sm text-brand-muted">
                      {displayName(booking.customerName, booking.customerPhone)} ·{" "}
                      <PhoneText value={booking.customerPhone} /> · {booking.mode}
                    </span>
                  </span>
                </Link>
              </li>
            ))}
        </ul>
      ) : null}
      {sessions.isSuccess && sessions.data.total > sessions.data.items.length ? (
        <Link className="px-1 text-sm font-medium text-brand-primary hover:underline" to={todayLink}>
          {sessions.data.total - sessions.data.items.length} more today
        </Link>
      ) : null}
    </Panel>
  );
}

function DashboardSkeleton() {
  return (
    <div className="flex flex-col gap-6" aria-hidden="true">
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        {Array.from({ length: 6 }, (_, index) => (
          <Skeleton key={index} variant="rounded" height={112} sx={{ borderRadius: "24px" }} />
        ))}
      </div>
      <div className="grid gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
        <Skeleton variant="rounded" height={280} sx={{ borderRadius: "24px" }} />
        <Skeleton variant="rounded" height={280} sx={{ borderRadius: "24px" }} />
      </div>
    </div>
  );
}

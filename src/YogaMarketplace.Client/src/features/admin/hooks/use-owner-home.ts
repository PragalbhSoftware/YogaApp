import { useAdminBookings, useAdminSettings, useAdminSummary } from "@/features/admin/hooks/use-admin";
import { bookingStatusCount } from "@/features/admin/utils";
import { kolkataToday } from "@/utils/clock";

/** The largest page the API serves; a day with more sessions links to the full list. */
const TODAY_PAGE_SIZE = 100;

export function useOwnerHome() {
  const today = kolkataToday();
  const summary = useAdminSummary();
  const settings = useAdminSettings();
  const todayUpcoming = useAdminBookings({ status: "Upcoming", from: today, to: today, pageSize: TODAY_PAGE_SIZE });

  const queries = [summary, settings];
  const isLoading = queries.some((query) => query.isLoading);
  const firstError = queries.find((query) => query.isError);

  function refetch() {
    for (const query of queries) {
      void query.refetch();
    }
    void todayUpcoming.refetch();
  }

  const report = summary.data;
  const bookingsByStatus = report?.bookingsByStatus ?? [];

  return {
    isLoading,
    error: firstError?.error,
    refetch,
    today,
    takeRate: settings.data?.commissionPercent ?? null,
    yourFee: report?.platformFees ?? 0,
    oweInstructors: report?.pendingPayouts.net ?? 0,
    gmvPaid: report?.gmvPaid ?? 0,
    packSales: report?.packSales ?? 0,
    revenue: report?.revenue ?? 0,
    paidBookings: report?.paidBookings ?? 0,
    creditBookings: report?.creditBookings ?? 0,
    pendingInstructorCount: report?.pendingInstructors ?? 0,
    pendingAcceptCount: bookingStatusCount(bookingsByStatus, "PendingAccept"),
    upcomingCount: bookingStatusCount(bookingsByStatus, "Upcoming"),
    payoutsToSend: report?.pendingPayouts.count ?? 0,
    todaySessions: todayUpcoming,
  };
}

import { useAdminBookings, useAdminPayouts, useAdminPolicy, useAdminProviders, useAdminSummary } from "@/features/admin/hooks/use-admin";
import { bookingStatusCount, sumPayoutFees } from "@/features/admin/utils";
import { kolkataToday } from "@/utils/clock";

export function useOwnerHome() {
  const today = kolkataToday();
  const summary = useAdminSummary();
  const policy = useAdminPolicy();
  const pendingInstructors = useAdminProviders("Pending");
  const pendingPayouts = useAdminPayouts("Pending");
  const exportedPayouts = useAdminPayouts("Exported");
  const paidPayouts = useAdminPayouts("Paid");
  const todayUpcoming = useAdminBookings({ status: "Upcoming", from: today, to: today });

  const queries = [summary, policy, pendingInstructors, pendingPayouts, exportedPayouts, paidPayouts];
  const isLoading = queries.some((query) => query.isLoading);
  const firstError = queries.find((query) => query.isError);

  function refetch() {
    for (const query of queries) {
      void query.refetch();
    }
    void todayUpcoming.refetch();
  }

  const payouts = [
    ...(pendingPayouts.data ?? []),
    ...(exportedPayouts.data ?? []),
    ...(paidPayouts.data ?? []),
  ];

  const report = summary.data;
  const bookingsByStatus = report?.bookingsByStatus ?? [];

  return {
    isLoading,
    error: firstError?.error,
    refetch,
    takeRate: policy.data?.platformFeePercent ?? null,
    yourFee: sumPayoutFees(payouts),
    oweInstructors: report?.pendingPayouts.net ?? 0,
    gmvPaid: report?.gmvPaid ?? 0,
    pendingInstructorCount: pendingInstructors.data?.length ?? 0,
    pendingAcceptCount: bookingStatusCount(bookingsByStatus, "PendingAccept"),
    upcomingCount: bookingStatusCount(bookingsByStatus, "Upcoming"),
    payoutsToSend: report?.pendingPayouts.count ?? 0,
    todaySessions: todayUpcoming,
  };
}

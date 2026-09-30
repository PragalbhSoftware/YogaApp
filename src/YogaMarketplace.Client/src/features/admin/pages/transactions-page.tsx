import { useSearchParams } from "react-router-dom";
import { Tabs, Tab } from "@mui/material";
import { toast } from "sonner";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { BookingListSkeleton } from "@/components/common/loading-skeleton";
import { PhoneText } from "@/components/common/phone-text";
import { PriceText } from "@/components/common/price-text";
import { AdminPage } from "@/features/admin/components/admin-page";
import { FilterChips } from "@/features/admin/components/filter-chips";
import { PayoutCard } from "@/features/admin/components/payout-card";
import { useAdminPayments, useAdminPayouts, useMarkPayoutPaid } from "@/features/admin/hooks/use-admin";
import { paymentStatuses, payoutStatuses } from "@/features/admin/types";
import { displayName, formatAdminWhen, listCap } from "@/features/admin/utils";
import { PayoutExportBar } from "@/features/admin/components/payout-export-bar";
import { paymentStatusLabel } from "@/features/bookings/utils/status";
import { toUserMessage } from "@/services/http/api-error";

const paymentFilters = [
  { value: null, label: "All" },
  ...paymentStatuses.map((status) => ({ value: status, label: paymentStatusLabel(status) })),
];

const payoutFilters = payoutStatuses.map((status) => ({ value: status, label: status }));

export function TransactionsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const tab = searchParams.get("tab") === "payouts" ? "payouts" : "payments";
  const paymentStatus = paymentStatuses.includes(searchParams.get("status") as (typeof paymentStatuses)[number])
    ? searchParams.get("status") ?? undefined
    : undefined;
  const payoutStatus = payoutStatuses.includes(searchParams.get("payout") as (typeof payoutStatuses)[number])
    ? searchParams.get("payout") ?? "Pending"
    : "Pending";

  const payments = useAdminPayments(paymentStatus, tab === "payments");
  const payouts = useAdminPayouts(payoutStatus, tab === "payouts");
  const markPaid = useMarkPayoutPaid();

  function setTab(next: string) {
    const params = new URLSearchParams(searchParams);
    params.set("tab", next);
    setSearchParams(params, { replace: true });
  }

  return (
    <AdminPage
      kicker="Money"
      title="Transactions"
      lead="Customer payments stay here. When a session is completed, marked no-show, or cancelled late, a payout appears so you can send the instructor their share and keep your take."
      note={listCap}
    >
      <Tabs
        value={tab}
        onChange={(_, next) => setTab(next)}
        aria-label="Transaction type"
        sx={{
          minHeight: 48,
          "& .MuiTab-root": { fontFamily: "inherit", fontWeight: 600, textTransform: "none" },
        }}
      >
        <Tab value="payments" label="Payments" />
        <Tab value="payouts" label="Payouts" />
      </Tabs>

      {tab === "payments" ? (
        <>
          <FilterChips
            value={paymentStatus ?? null}
            options={paymentFilters}
            onChange={(next) => {
              const params = new URLSearchParams(searchParams);
              if (next) params.set("status", next);
              else params.delete("status");
              setSearchParams(params, { replace: true });
            }}
            label="Payment status"
          />
          {payments.isLoading ? <BookingListSkeleton /> : null}
          {payments.isError ? (
            <ErrorState message={toUserMessage(payments.error)} onRetry={() => void payments.refetch()} />
          ) : null}
          {payments.isSuccess && payments.data.length === 0 ? (
            <EmptyState title="No payments in this list" description="Paid bookings appear here after checkout." />
          ) : null}
          {payments.isSuccess
            ? payments.data.map((payment) => (
                <article
                  key={payment.id}
                  className="rounded-[24px] border border-brand-border bg-brand-surface p-5"
                >
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <h2 className="font-heading text-lg font-medium">{payment.providerName}</h2>
                      <p className="mt-1 text-sm text-brand-muted">
                        {displayName(payment.customerName, payment.customerPhone)} ·{" "}
                        <PhoneText value={payment.customerPhone} />
                      </p>
                    </div>
                    <p className="text-[11px] font-semibold tracking-[0.14em] text-brand-primary uppercase">
                      {paymentStatusLabel(payment.status)}
                    </p>
                  </div>
                  <p className="mt-3">
                    <PriceText amount={payment.amount} /> · {payment.currency}
                  </p>
                  {payment.refundedAmount > 0 ? (
                    <p className="mt-1 text-sm text-brand-muted">
                      Refunded <PriceText amount={payment.refundedAmount} className="min-w-0 text-sm" />
                    </p>
                  ) : null}
                  <p className="mt-1 text-sm text-brand-muted">{formatAdminWhen(payment.createdAt)}</p>
                  {payment.gatewayPaymentId ? (
                    <p className="mt-1 text-xs text-brand-muted">{payment.gatewayPaymentId}</p>
                  ) : null}
                </article>
              ))
            : null}
        </>
      ) : (
        <>
          <FilterChips
            value={payoutStatus}
            options={payoutFilters}
            onChange={(next) => {
              const params = new URLSearchParams(searchParams);
              params.set("payout", next ?? "Pending");
              setSearchParams(params, { replace: true });
            }}
            label="Payout status"
          />
          <PayoutExportBar status={payoutStatus} rowCount={payouts.data?.length ?? 0} />
          {payouts.isLoading ? <BookingListSkeleton /> : null}
          {payouts.isError ? (
            <ErrorState message={toUserMessage(payouts.error)} onRetry={() => void payouts.refetch()} />
          ) : null}
          {payouts.isSuccess && payouts.data.length === 0 ? (
            <EmptyState
              title="No payouts in this list"
              description="A payout is created when a session is completed, marked no-show, or cancelled late."
            />
          ) : null}
          {payouts.isSuccess
            ? payouts.data.map((payout) => (
                <PayoutCard
                  key={payout.id}
                  payout={payout}
                  busy={markPaid.isPending && markPaid.variables === payout.id}
                  onMarkPaid={(id) => {
                    markPaid.mutate(id, {
                      onSuccess: () => toast.success("Marked paid. This amount drops out of what you owe."),
                      onError: (error) => toast.error(toUserMessage(error)),
                    });
                  }}
                />
              ))
            : null}
        </>
      )}
    </AdminPage>
  );
}

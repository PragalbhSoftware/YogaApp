import { Tab, Tabs } from "@mui/material";
import { AdminPage } from "@/features/admin/components/admin-page";
import { PaymentsList } from "@/features/admin/components/payments-list";
import { PayoutsList } from "@/features/admin/components/payouts-list";
import { useListParams } from "@/features/admin/hooks/use-list-params";

type MoneyTab = "payments" | "payouts";

export function TransactionsPage() {
  const { searchParams, update } = useListParams();
  const tab: MoneyTab = searchParams.get("tab") === "payouts" ? "payouts" : "payments";

  return (
    <AdminPage
      kicker="Money"
      title="Transactions"
      lead="Customer payments and pack sales stay here. When a session is completed, marked no-show, or cancelled late, a payout appears so you can send the instructor their share and keep your take."
    >
      <Tabs
        value={tab}
        onChange={(_, next: MoneyTab) => update({ tab: next })}
        aria-label="Transaction type"
        sx={{
          minHeight: 48,
          "& .MuiTab-root": { fontFamily: "inherit", fontWeight: 600, textTransform: "none" },
        }}
      >
        <Tab value="payments" label="Payments" />
        <Tab value="payouts" label="Payouts" />
      </Tabs>

      {tab === "payments" ? <PaymentsList /> : <PayoutsList />}
    </AdminPage>
  );
}

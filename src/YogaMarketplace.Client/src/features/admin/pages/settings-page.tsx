import { Tab, Tabs } from "@mui/material";
import { useSearchParams } from "react-router-dom";
import { AdminPage } from "@/features/admin/components/admin-page";
import { AreaSettings } from "@/features/admin/components/area-settings";
import { BannerSettings } from "@/features/admin/components/banner-settings";
import { CategorySettings } from "@/features/admin/components/category-settings";
import { CreditRulesSettings } from "@/features/admin/components/credit-rules-settings";
import { ErrorAlertSettings } from "@/features/admin/components/error-alert-settings";
import { FeeSettings } from "@/features/admin/components/fee-settings";
import { NotificationSettings } from "@/features/admin/components/notification-settings";
import { PackSettings } from "@/features/admin/components/pack-settings";
import { PageSettings } from "@/features/admin/components/page-settings";
import { PrivacySettings } from "@/features/admin/components/privacy-settings";
import { ReconciliationSettings } from "@/features/admin/components/reconciliation-settings";

const tabs = ["earnings", "packs", "areas", "banner", "catalog", "pages", "alerts"] as const;
type SettingsTab = (typeof tabs)[number];

function readTab(value: string | null): SettingsTab {
  return tabs.includes(value as SettingsTab) ? (value as SettingsTab) : "earnings";
}

export function SettingsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const tab = readTab(searchParams.get("tab"));

  return (
    <AdminPage
      kicker="Owner"
      title="Settings"
      lead="Fees and payouts, session packs and credit rules, the neighbourhoods customers can pick, the home banner, the yoga label on browse, the policy pages, error alerts and booking emails."
    >
      <Tabs
        value={tab}
        onChange={(_, next: SettingsTab) => {
          const params = new URLSearchParams(searchParams);
          params.set("tab", next);
          setSearchParams(params, { replace: true });
        }}
        aria-label="Settings"
        variant="scrollable"
        allowScrollButtonsMobile
        sx={{
          minHeight: 48,
          "& .MuiTab-root": { fontFamily: "inherit", fontWeight: 600, textTransform: "none" },
        }}
      >
        <Tab value="earnings" label="Fees" />
        <Tab value="packs" label="Packs" />
        <Tab value="areas" label="Areas" />
        <Tab value="banner" label="Banner" />
        <Tab value="catalog" label="Catalog" />
        <Tab value="pages" label="Pages" />
        <Tab value="alerts" label="Alerts" />
      </Tabs>

      {tab === "earnings" ? (
        <div className="space-y-8">
          <FeeSettings />
          <ReconciliationSettings />
        </div>
      ) : null}
      {tab === "packs" ? (
        <div className="space-y-8">
          <PackSettings />
          <CreditRulesSettings />
        </div>
      ) : null}
      {tab === "areas" ? <AreaSettings /> : null}
      {tab === "banner" ? <BannerSettings /> : null}
      {tab === "catalog" ? <CategorySettings /> : null}
      {tab === "pages" ? (
        <div className="space-y-8">
          <PageSettings />
          <PrivacySettings />
        </div>
      ) : null}
      {tab === "alerts" ? (
        <div className="space-y-8">
          <ErrorAlertSettings />
          <NotificationSettings />
        </div>
      ) : null}
    </AdminPage>
  );
}

import { Tab, Tabs } from "@mui/material";
import { useSearchParams } from "react-router-dom";
import { AdminPage } from "@/features/admin/components/admin-page";
import { AreaSettings } from "@/features/admin/components/area-settings";
import { BannerSettings } from "@/features/admin/components/banner-settings";
import { CategorySettings } from "@/features/admin/components/category-settings";
import { CreditRulesSettings } from "@/features/admin/components/credit-rules-settings";
import { FeeSettings } from "@/features/admin/components/fee-settings";
import { PackSettings } from "@/features/admin/components/pack-settings";
import { PageSettings } from "@/features/admin/components/page-settings";

const tabs = ["earnings", "packs", "areas", "banner", "catalog", "pages"] as const;
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
      lead="Fees and payouts, session packs and credit rules, the neighbourhoods customers can pick, the home banner, the yoga label on browse, and the policy pages."
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
      </Tabs>

      {tab === "earnings" ? <FeeSettings /> : null}
      {tab === "packs" ? (
        <div className="space-y-8">
          <PackSettings />
          <CreditRulesSettings />
        </div>
      ) : null}
      {tab === "areas" ? <AreaSettings /> : null}
      {tab === "banner" ? <BannerSettings /> : null}
      {tab === "catalog" ? <CategorySettings /> : null}
      {tab === "pages" ? <PageSettings /> : null}
    </AdminPage>
  );
}

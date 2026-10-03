import { Tab, Tabs } from "@mui/material";
import { useSearchParams } from "react-router-dom";
import { AdminPage } from "@/features/admin/components/admin-page";
import { AreaSettings } from "@/features/admin/components/area-settings";
import { BannerSettings } from "@/features/admin/components/banner-settings";
import { CategorySettings } from "@/features/admin/components/category-settings";
import { FeeSettings } from "@/features/admin/components/fee-settings";

const tabs = ["earnings", "areas", "banner", "catalog"] as const;
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
      lead="Fees and payouts, the neighbourhoods customers can pick, the home banner, and the yoga label on browse."
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
        <Tab value="areas" label="Areas" />
        <Tab value="banner" label="Banner" />
        <Tab value="catalog" label="Catalog" />
      </Tabs>

      {tab === "earnings" ? <FeeSettings /> : null}
      {tab === "areas" ? <AreaSettings /> : null}
      {tab === "banner" ? <BannerSettings /> : null}
      {tab === "catalog" ? <CategorySettings /> : null}
    </AdminPage>
  );
}

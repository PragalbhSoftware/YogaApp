import { Tab, Tabs } from "@mui/material";
import { useSearchParams } from "react-router-dom";
import { AdminPage } from "@/features/admin/components/admin-page";
import { AreaSettings } from "@/features/admin/components/area-settings";
import { CategorySettings } from "@/features/admin/components/category-settings";
import { PolicySettings } from "@/features/admin/components/policy-settings";

const tabs = ["earnings", "areas", "catalog"] as const;
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
      lead="Your take rate, the cities and neighbourhoods customers can pick, and the yoga label on browse."
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
        <Tab value="earnings" label="Earnings" />
        <Tab value="areas" label="Cities" />
        <Tab value="catalog" label="Catalog" />
      </Tabs>

      {tab === "earnings" ? <PolicySettings /> : null}
      {tab === "areas" ? <AreaSettings /> : null}
      {tab === "catalog" ? <CategorySettings /> : null}
    </AdminPage>
  );
}

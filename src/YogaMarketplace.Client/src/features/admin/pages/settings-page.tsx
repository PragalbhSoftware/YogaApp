import { Tab, Tabs, useMediaQuery, useTheme } from "@mui/material";
import { useSearchParams } from "react-router-dom";
import { AdminPage } from "@/features/admin/components/admin-page";
import { AreaSettings } from "@/features/admin/components/area-settings";
import { BannerSettings } from "@/features/admin/components/banner-settings";
import { CategorySettings } from "@/features/admin/components/category-settings";
import { CreditRulesSettings } from "@/features/admin/components/credit-rules-settings";
import { FeeSettings } from "@/features/admin/components/fee-settings";
import { PackSettings } from "@/features/admin/components/pack-settings";
import { PageSettings } from "@/features/admin/components/page-settings";

const tabs = [
  { value: "earnings", label: "Fees and payouts" },
  { value: "packs", label: "Packs and credits" },
  { value: "areas", label: "Areas" },
  { value: "banner", label: "Home banner" },
  { value: "catalog", label: "Catalog" },
  { value: "pages", label: "Policy pages" },
] as const;

type SettingsTab = (typeof tabs)[number]["value"];

function readTab(value: string | null): SettingsTab {
  return tabs.find((tab) => tab.value === value)?.value ?? "earnings";
}

export function SettingsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const tab = readTab(searchParams.get("tab"));
  const theme = useTheme();
  const desktop = useMediaQuery(theme.breakpoints.up("lg"));

  return (
    <AdminPage
      kicker="Owner"
      title="Settings"
      lead="Fees and payouts, session packs and credit rules, the neighbourhoods customers can pick, the home banner, the yoga label on browse, and the policy pages."
    >
      <div className="grid gap-6 lg:grid-cols-[13rem_minmax(0,1fr)] lg:items-start">
        <Tabs
          value={tab}
          orientation={desktop ? "vertical" : "horizontal"}
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
            ...(desktop && {
              position: "sticky",
              top: 24,
              borderRight: "1px solid",
              borderColor: "divider",
            }),
            "& .MuiTab-root": {
              fontFamily: "inherit",
              fontWeight: 600,
              textTransform: "none",
              ...(desktop && { alignItems: "flex-start", textAlign: "left", minHeight: 44 }),
            },
          }}
        >
          {tabs.map((item) => (
            <Tab key={item.value} value={item.value} label={item.label} />
          ))}
        </Tabs>

        <div className="min-w-0">
          {tab === "earnings" ? <FeeSettings /> : null}
          {tab === "packs" ? (
            <div className="flex flex-col gap-8">
              <PackSettings />
              <CreditRulesSettings />
            </div>
          ) : null}
          {tab === "areas" ? <AreaSettings /> : null}
          {tab === "banner" ? <BannerSettings /> : null}
          {tab === "catalog" ? <CategorySettings /> : null}
          {tab === "pages" ? <PageSettings /> : null}
        </div>
      </div>
    </AdminPage>
  );
}

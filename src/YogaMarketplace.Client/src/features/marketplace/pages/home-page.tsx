import { useEffect, useState, useSyncExternalStore } from "react";
import { useSearchParams } from "react-router-dom";
import { sessionModes, type SessionModeFilter } from "@/constants/catalog";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { PageLoader } from "@/components/common/page-loader";
import { AreaPicker } from "@/features/marketplace/components/area-picker";
import { HomeHero } from "@/features/marketplace/components/home-hero";
import { InstructorList } from "@/features/marketplace/components/instructor-list";
import { ModeFilter } from "@/features/marketplace/components/mode-filter";
import { useAreas } from "@/features/marketplace/hooks/use-areas";
import { useClosedAreaReset } from "@/features/marketplace/hooks/use-closed-area-reset";
import { useCities } from "@/features/marketplace/hooks/use-cities";
import { useInstructors } from "@/features/marketplace/hooks/use-instructors";
import { useSiteBanner } from "@/features/marketplace/hooks/use-site";
import { useCityHydrated } from "@/hooks/use-city-hydrated";
import { usePageTitle } from "@/hooks/use-page-title";
import { ApiError, toUserMessage } from "@/services/http/api-error";
import { useAreaStore } from "@/stores/area-store";
import { useCityStore } from "@/stores/city-store";

function readMode(value: string | null): SessionModeFilter {
  if (value && sessionModes.includes(value as (typeof sessionModes)[number])) {
    return value as SessionModeFilter;
  }
  return "";
}

function useAreaHydrated() {
  return useSyncExternalStore(
    (onChange) => useAreaStore.persist.onFinishHydration(onChange),
    () => useAreaStore.persist.hasHydrated(),
    () => false,
  );
}

export function HomePage() {
  const cityId = useCityStore((state) => state.cityId);
  const clearCity = useCityStore((state) => state.clearCity);
  const areaName = useAreaStore((state) => state.areaName);
  const setArea = useAreaStore((state) => state.setArea);
  const clearArea = useAreaStore((state) => state.clearArea);
  const areaHydrated = useAreaHydrated();
  const cityHydrated = useCityHydrated();
  const cities = useCities();
  const city = cities.data?.find((item) => item.id === cityId)?.name ?? null;
  const areas = useAreas(cityId);
  const areaId = areas.data?.find((item) => item.name === areaName)?.id ?? null;
  const [changingArea, setChangingArea] = useState(false);
  const [searchParams, setSearchParams] = useSearchParams();
  const mode = readMode(searchParams.get("mode"));
  const hydrated = areaHydrated && cityHydrated;
  const pickingArea = Boolean(hydrated && cityId && city && (!areaName || changingArea));
  const instructors = useInstructors(pickingArea ? null : cityId, pickingArea ? null : areaId, mode);
  const banner = useSiteBanner();
  useClosedAreaReset(hydrated ? cityId : null, hydrated ? city : null, hydrated ? areaName : null);
  usePageTitle(city && areaName && !pickingArea ? `Instructors in ${city}` : null);

  useEffect(() => {
    if (!(instructors.error instanceof ApiError) || instructors.error.status !== 400) return;
    clearCity();
    clearArea();
  }, [instructors.error, clearCity, clearArea]);

  if (!hydrated || cities.isLoading) return <PageLoader />;
  if (cities.isError) {
    return (
      <main className="mx-auto max-w-3xl px-4 py-8 sm:px-8">
        <ErrorState message={toUserMessage(cities.error)} onRetry={() => void cities.refetch()} />
      </main>
    );
  }
  if (!cities.data?.length) {
    return (
      <main className="mx-auto max-w-3xl px-4 py-8 sm:px-8">
        <EmptyState
          title="We're not live in any city yet"
          description="Cities will show up here once the marketplace lists them."
        />
      </main>
    );
  }

  if (!cityId || !city || !areaName || changingArea) {
    if (!cityId || !city) return <PageLoader />;
    return (
      <AreaPicker
        cityId={cityId}
        selectedCity={city}
        selectedName={areaName}
        onSelect={(_nextCity, name) => {
          setArea(name);
          setChangingArea(false);
        }}
        onCancel={areaName ? () => setChangingArea(false) : undefined}
      />
    );
  }

  const modeLabel = mode || "any mode";

  return (
    <main>
      <HomeHero
        city={city}
        areaName={areaName}
        banner={banner.data}
        bannerLoading={banner.isLoading}
        onChangeArea={() => setChangingArea(true)}
      />
      <section className="mx-auto max-w-3xl space-y-5 px-4 py-6 sm:px-8">
        <ModeFilter
          value={mode}
          onChange={(next) => {
            const nextParams = new URLSearchParams(searchParams);
            if (next) nextParams.set("mode", next);
            else nextParams.delete("mode");
            setSearchParams(nextParams, { replace: true });
          }}
        />
        <InstructorList
          cityName={city}
          mode={mode}
          modeLabel={modeLabel}
          instructors={instructors.data}
          isLoading={instructors.isLoading}
          isError={instructors.isError}
          error={instructors.error}
          onRetry={() => void instructors.refetch()}
          onShowOnline={() => {
            const nextParams = new URLSearchParams(searchParams);
            nextParams.set("mode", "Online");
            setSearchParams(nextParams, { replace: true });
          }}
        />
      </section>
    </main>
  );
}

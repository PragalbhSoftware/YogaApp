import { useState, useSyncExternalStore } from "react";
import { useSearchParams } from "react-router-dom";
import { sessionModes, type SessionModeFilter } from "@/constants/catalog";
import { PageLoader } from "@/components/common/page-loader";
import { AreaPicker } from "@/features/marketplace/components/area-picker";
import { HomeHero } from "@/features/marketplace/components/home-hero";
import { InstructorList } from "@/features/marketplace/components/instructor-list";
import { ModeFilter } from "@/features/marketplace/components/mode-filter";
import { useInstructors } from "@/features/marketplace/hooks/use-instructors";
import { useAreaStore } from "@/stores/area-store";

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
  const city = useAreaStore((state) => state.city);
  const areaName = useAreaStore((state) => state.areaName);
  const setArea = useAreaStore((state) => state.setArea);
  const hydrated = useAreaHydrated();
  const [changingArea, setChangingArea] = useState(false);
  const [searchParams, setSearchParams] = useSearchParams();
  const mode = readMode(searchParams.get("mode"));
  const pickingArea = Boolean(hydrated && (!city || !areaName || changingArea));
  const instructors = useInstructors(pickingArea ? null : city, pickingArea ? null : areaName, mode);

  if (!hydrated) return <PageLoader />;

  if (!city || !areaName || changingArea) {
    return (
      <AreaPicker
        selectedCity={city}
        selectedName={areaName}
        onSelect={(nextCity, name) => {
          setArea(nextCity, name);
          setChangingArea(false);
        }}
        onCancel={city && areaName ? () => setChangingArea(false) : undefined}
      />
    );
  }

  const modeLabel = mode || "any mode";

  return (
    <main>
      <HomeHero city={city} areaName={areaName} onChangeArea={() => setChangingArea(true)} />
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
          areaName={areaName}
          mode={mode}
          modeLabel={modeLabel}
          instructors={instructors.data}
          isLoading={instructors.isLoading}
          isError={instructors.isError}
          error={instructors.error}
          onRetry={() => void instructors.refetch()}
          onChangeArea={() => setChangingArea(true)}
        />
      </section>
    </main>
  );
}

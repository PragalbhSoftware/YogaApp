import { useCallback, useEffect, useSyncExternalStore } from "react";
import { CityPickerDialog } from "@/features/marketplace/components/city-picker-dialog";
import { showCityPicker } from "@/features/marketplace/components/show-city-picker";
import { useCities } from "@/features/marketplace/hooks/use-cities";
import type { CatalogCity } from "@/features/marketplace/types";
import { useAreaStore } from "@/stores/area-store";
import { useCityStore } from "@/stores/city-store";

function useCityHydrated() {
  return useSyncExternalStore(
    (onChange) => useCityStore.persist.onFinishHydration(onChange),
    () => useCityStore.persist.hasHydrated(),
    () => false,
  );
}

export function CustomerCityPicker() {
  const hydrated = useCityHydrated();
  const cityId = useCityStore((state) => state.cityId);
  const setCityId = useCityStore((state) => state.setCityId);
  const clearCity = useCityStore((state) => state.clearCity);
  const clearArea = useAreaStore((state) => state.clearArea);
  const cities = useCities();
  const clearSaved = useCallback(() => {
    clearCity();
    clearArea();
  }, [clearCity, clearArea]);
  const choose = useCallback(
    (id: string) => {
      clearArea();
      setCityId(id);
    },
    [clearArea, setCityId],
  );

  return (
    <CustomerCityPickerBody
      hydrated={hydrated}
      cityId={cityId}
      cities={cities.data}
      citiesLoaded={cities.isSuccess}
      onClear={clearSaved}
      onSelect={choose}
    />
  );
}

type CustomerCityPickerBodyProps = {
  hydrated: boolean;
  cityId: string | null;
  cities: CatalogCity[] | undefined;
  citiesLoaded: boolean;
  onClear: () => void;
  onSelect: (cityId: string) => void;
};

export function CustomerCityPickerBody({
  hydrated,
  cityId,
  cities,
  citiesLoaded,
  onClear,
  onSelect,
}: CustomerCityPickerBodyProps) {
  useEffect(() => {
    if (!hydrated || !citiesLoaded || !cityId || !cities) return;
    if (cities.some((city) => city.id === cityId)) return;
    onClear();
  }, [hydrated, citiesLoaded, cityId, cities, onClear]);

  const listed = cities ?? [];
  if (!showCityPicker(hydrated, citiesLoaded, cityId, listed)) return null;
  return <CityPickerDialog open cities={listed} onSelect={onSelect} />;
}

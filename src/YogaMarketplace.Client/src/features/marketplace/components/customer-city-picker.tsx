import { useCallback } from "react";
import { CustomerCityPickerBody } from "@/features/marketplace/components/customer-city-picker-body";
import { useCities } from "@/features/marketplace/hooks/use-cities";
import { useCityHydrated } from "@/hooks/use-city-hydrated";
import { useAreaStore } from "@/stores/area-store";
import { useCityStore } from "@/stores/city-store";

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

import { useCallback } from "react";
import { CustomerCityPickerBody } from "@/features/marketplace/components/customer-city-picker-body";
import { useCities } from "@/features/marketplace/hooks/use-cities";
import { chooseCity } from "@/features/marketplace/utils/choose-city";
import { useCityHydrated } from "@/hooks/use-city-hydrated";
import { useAreaStore } from "@/stores/area-store";
import { useCityStore } from "@/stores/city-store";

export function CustomerCityPicker() {
  const hydrated = useCityHydrated();
  const cityId = useCityStore((state) => state.cityId);
  const clearCity = useCityStore((state) => state.clearCity);
  const clearArea = useAreaStore((state) => state.clearArea);
  const cities = useCities();
  const clearSaved = useCallback(() => {
    clearCity();
    clearArea();
  }, [clearCity, clearArea]);

  return (
    <CustomerCityPickerBody
      hydrated={hydrated}
      cityId={cityId}
      cities={cities.data}
      citiesLoaded={cities.isSuccess}
      onClear={clearSaved}
      onSelect={(id) => void chooseCity(id)}
    />
  );
}

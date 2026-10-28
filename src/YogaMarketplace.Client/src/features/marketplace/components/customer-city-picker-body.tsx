import { useEffect } from "react";
import { CityPickerDialog } from "@/features/marketplace/components/city-picker-dialog";
import { showCityPicker } from "@/features/marketplace/components/show-city-picker";
import type { CatalogCity } from "@/features/marketplace/types";

export type CustomerCityPickerBodyProps = {
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

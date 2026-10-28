export function showCityPicker(hydrated: boolean, citiesLoaded: boolean, cityId: string | null, cities: { id: string }[]) {
  if (!hydrated || !citiesLoaded || cities.length === 0) return false;
  if (!cityId) return true;
  return !cities.some((city) => city.id === cityId);
}

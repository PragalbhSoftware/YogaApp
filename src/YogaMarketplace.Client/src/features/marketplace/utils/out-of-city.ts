/** True when the instructor is outside the customer's selected marketplace city. */
export function isOutOfSelectedCity(
  providerCityId: string | null | undefined,
  selectedCityId: string | null | undefined,
) {
  return Boolean(providerCityId && selectedCityId && providerCityId !== selectedCityId);
}

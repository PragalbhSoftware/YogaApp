/**
 * Product geography.
 *
 * Yoga Marketplace is an India product. First live city is Mumbai; more
 * Indian cities follow in phases. Customer-facing copy must not lock the
 * brand to one city.
 *
 * The owner opens cities from admin Settings; `firstCity` is only the
 * default for new neighbourhoods and for pre-city saved picks. Do not
 * treat it as the product name or login headline.
 */
export const launch = {
  market: "India",
  firstCity: "Mumbai",
  heroChip: "India · Yoga",
  locationHighlight: "In your city",
} as const;

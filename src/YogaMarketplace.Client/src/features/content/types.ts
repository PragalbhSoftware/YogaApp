export const contentSlugs = ["refund-policy", "terms", "privacy"] as const;
export type ContentSlug = (typeof contentSlugs)[number];

export type ContentPage = {
  slug: ContentSlug;
  title: string;
  body: string;
  version: number;
  updatedAt: string;
};

export type UpdateContentPageInput = {
  slug: ContentSlug;
  title: string;
  body: string;
  version: number;
};

export const contentPageLimits = {
  titleMax: 120,
  bodyMax: 20_000,
} as const;

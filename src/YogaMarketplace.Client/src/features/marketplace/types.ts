export type CatalogCity = {
  id: string;
  name: string;
  stateId: string;
  slug: string;
  displayOrder: number;
};

export type Area = {
  id: string;
  cityId: string;
  cityName: string;
  stateId: string;
  stateName: string;
  pincode: string;
  name: string;
};

export type PublicPolicy = {
  currency: string;
  commissionPercent: number;
  convenienceFee: number;
  cancelFreeWindowHours: number;
  rescheduleFreeWindowHours: number;
  lateCancelFeeType: "Percent" | "Flat";
  lateCancelFeeValue: number;
};

export type SiteBanner = {
  title: string | null;
  subtitle: string | null;
  offer: string | null;
};

export type ModeRate = {
  mode: string;
  rate: number;
};

export type InstructorSummary = {
  id: string;
  displayName: string;
  bio: string | null;
  area: string;
  city: string;
  status: string;
  modes: ModeRate[] | null;
  ratingAverage: number | null;
  reviewCount: number;
  /** API path of the first profile photo. */
  photoUrl?: string | null;
  /** True once admin has approved at least one certificate. */
  isVerified: boolean;
  /** True when this row is Online from another city for the selected city. */
  isOutOfCity?: boolean;
};

export type InstructorDetail = Omit<InstructorSummary, "photoUrl"> & {
  age: number | null;
  studioAddress: string | null;
  /** False when admin has closed the instructor's area. The profile stays viewable but cannot be booked. */
  areaOpen: boolean;
  photoUrls: string[];
  introVideoId: string | null;
  /** Titles of admin-approved certificates. The files themselves are never public. */
  certificates: string[];
  /** One discounted or free first session per customer, when the instructor offers it. */
  trial: TrialOffer | null;
};

export type TrialOffer = {
  serviceId: string;
  price: number;
};

export type OpenSlot = {
  id: string;
  mode: string;
  date: string;
  start: string;
  end: string;
};

export type OpenSlotList = {
  mode: string;
  from: string;
  to: string;
  slots: OpenSlot[] | null;
};

export type PublicReview = {
  rating: number;
  comment: string | null;
  reviewerName: string;
  createdAt: string;
};

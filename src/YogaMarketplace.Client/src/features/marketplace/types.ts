export type Area = {
  id: string;
  city: string;
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
};

export type InstructorDetail = InstructorSummary & {
  age: number | null;
  studioAddress: string | null;
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

import { z } from "zod";
import { lateCancelFeeTypes, payoutCycles } from "@/features/admin/types";

const nameSchema = z
  .string()
  .trim()
  .min(2, "Name must be 2 to 80 characters.")
  .max(80, "Name must be 2 to 80 characters.");

export const rejectSchema = z.object({
  reason: z.string().trim().max(300, "Keep the reason to 300 characters.").optional().or(z.literal("")),
});

export const adminCancelSchema = z.object({
  reason: z
    .string()
    .trim()
    .min(5, "Tell the customer why, in at least 5 characters.")
    .max(300, "Keep the reason to 300 characters."),
});

export function userBlockSchema(reasonRequired: boolean) {
  return z.object({
    reason: z
      .string()
      .trim()
      .max(300, "Keep the reason to 300 characters.")
      .refine((value) => !reasonRequired || value.length >= 5, "Give a reason of at least 5 characters."),
  });
}

export const areaSchema = z.object({
  name: nameSchema,
  city: z
    .string()
    .trim()
    .min(1, "Pick a city.")
    .min(2, "City must be 2 to 40 characters.")
    .max(40, "City must be 2 to 40 characters.")
    .regex(/^[A-Za-z .-]+$/, "Use letters, spaces, hyphens or dots only."),
});

export const renameSchema = z.object({
  name: nameSchema,
});

export const areaEditSchema = renameSchema.extend({
  isActive: z.boolean(),
});

export const settingsLimits = {
  maxWindowHours: 168,
  maxFlatAmount: 10_000,
  policyNoteMax: 400,
  bannerTitleMax: 80,
  bannerSubtitleMax: 160,
  bannerOfferMax: 60,
} as const;

const twoDecimals = (value: number) => /^-?\d+(\.\d{1,2})?$/.test(String(value));

const percent = z.coerce
  .number({ invalid_type_error: "Enter a number." })
  .min(0, "Must be between 0 and 100.")
  .max(100, "Must be between 0 and 100.")
  .refine(twoDecimals, "Use at most 2 decimal places.");

const flatAmount = z.coerce
  .number({ invalid_type_error: "Enter an amount." })
  .min(0, `Must be between 0 and ${settingsLimits.maxFlatAmount}.`)
  .max(settingsLimits.maxFlatAmount, `Must be between 0 and ${settingsLimits.maxFlatAmount}.`)
  .refine(twoDecimals, "Use at most 2 decimal places.");

const windowHours = z.coerce
  .number({ invalid_type_error: "Enter a number of hours." })
  .int("Enter a whole number of hours.")
  .min(0, `Must be between 0 and ${settingsLimits.maxWindowHours} hours.`)
  .max(settingsLimits.maxWindowHours, `Must be between 0 and ${settingsLimits.maxWindowHours} hours.`);

export const feeSettingsSchema = z
  .object({
    commissionPercent: percent,
    convenienceFee: flatAmount,
    cancelFreeWindowHours: windowHours,
    rescheduleFreeWindowHours: windowHours,
    lateCancelFeeType: z.enum(lateCancelFeeTypes),
    lateCancelFeeValue: flatAmount,
    payoutCycle: z.enum(payoutCycles),
    policyNote: z.string().trim().max(settingsLimits.policyNoteMax, "Keep the note to 400 characters."),
  })
  .refine((values) => values.lateCancelFeeType === "Flat" || values.lateCancelFeeValue <= 100, {
    path: ["lateCancelFeeValue"],
    message: "A percent fee must be between 0 and 100.",
  });

export const bannerSchema = z.object({
  bannerTitle: z
    .string()
    .trim()
    .max(settingsLimits.bannerTitleMax, `Keep the title to ${settingsLimits.bannerTitleMax} characters.`),
  bannerSubtitle: z
    .string()
    .trim()
    .max(settingsLimits.bannerSubtitleMax, `Keep the subtitle to ${settingsLimits.bannerSubtitleMax} characters.`),
  bannerOffer: z
    .string()
    .trim()
    .max(settingsLimits.bannerOfferMax, `Keep the offer to ${settingsLimits.bannerOfferMax} characters.`),
});

export type RejectFormValues = z.infer<typeof rejectSchema>;
export type AdminCancelValues = z.infer<typeof adminCancelSchema>;
export type UserBlockValues = z.infer<ReturnType<typeof userBlockSchema>>;
export type AreaFormValues = z.infer<typeof areaSchema>;
export type AreaEditValues = z.infer<typeof areaEditSchema>;
export type RenameFormValues = z.infer<typeof renameSchema>;
export type FeeSettingsValues = z.infer<typeof feeSettingsSchema>;
export type BannerValues = z.infer<typeof bannerSchema>;

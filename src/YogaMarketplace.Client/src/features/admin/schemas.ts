import { z } from "zod";
import { creditLateCancelRules, creditPayBases, lateCancelFeeTypes, payoutCycles } from "@/features/admin/types";

const nameSchema = z
  .string()
  .trim()
  .min(2, "Name must be 2 to 80 characters.")
  .max(80, "Name must be 2 to 80 characters.");

export const rejectSchema = z.object({
  reason: z.string().trim().max(300, "Keep the reason to 300 characters.").optional().or(z.literal("")),
});

export const certificateRejectSchema = z.object({
  reason: z
    .string()
    .trim()
    .min(1, "Say what the instructor should fix.")
    .max(300, "Keep the reason to 300 characters."),
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
  stateId: z.string().min(1, "Pick a state."),
  city: z.string().min(1, "Pick a city."),
});

const displayOrder = z.coerce
  .number({ invalid_type_error: "Enter a whole number." })
  .int("Enter a whole number.");

export const stateSchema = z.object({
  name: z
    .string()
    .trim()
    .min(2, "Name must be 2 to 80 characters.")
    .max(80, "Name must be 2 to 80 characters."),
  code: z
    .string()
    .trim()
    .min(2, "Code must be 2 to 10 letters.")
    .max(10, "Code must be 2 to 10 letters.")
    .regex(/^[A-Za-z]+$/, "Use letters only."),
  displayOrder,
});

export const citySchema = z.object({
  stateId: z.string().min(1, "Pick a state."),
  name: z
    .string()
    .trim()
    .min(2, "City must be 2 to 80 characters.")
    .max(80, "City must be 2 to 80 characters.")
    .regex(/^[A-Za-z0-9(][A-Za-z0-9 .()-]*$/, "Use letters, digits, spaces, hyphens, dots, or parentheses."),
  displayOrder,
});

export const renameSchema = z.object({
  name: nameSchema,
});

export const areaEditSchema = renameSchema.extend({
  isActive: z.boolean(),
});

export const stateEditSchema = stateSchema.extend({
  isActive: z.boolean(),
});

export const cityEditSchema = citySchema.omit({ stateId: true }).extend({
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
    lateCancelFeeType: z.enum(lateCancelFeeTypes),
    lateCancelFeeValue: flatAmount,
    payoutCycle: z.enum(payoutCycles),
    policyNote: z.string().trim().max(settingsLimits.policyNoteMax, "Keep the note to 400 characters."),
  })
  .refine((values) => values.lateCancelFeeType === "Flat" || values.lateCancelFeeValue <= 100, {
    path: ["lateCancelFeeValue"],
    message: "A percent fee must be between 0 and 100.",
  });

export const creditSettingsSchema = z.object({
  creditConvenienceFee: z.boolean(),
  creditPayBasis: z.enum(creditPayBases),
  creditLateCancel: z.enum(creditLateCancelRules),
  creditBookingStandsAfterExpiry: z.boolean(),
  packRefundsEnabled: z.boolean(),
  packReferenceSessionPrice: flatAmount,
  packRefundFee: flatAmount,
});

export const reconciliationLimits = {
  maxWebhookStuckMinutes: 7 * 24 * 60,
  maxRefundPendingHours: 30 * 24,
  noteMax: 500,
} as const;

const marketTime = z.string().regex(/^([01]\d|2[0-3]):[0-5]\d$/, "Pick a time such as 06:00.");

export const reconciliationSettingsSchema = z.object({
  reconciliationTime: marketTime,
  webhookStuckMinutes: z.coerce
    .number({ invalid_type_error: "Enter a number of minutes." })
    .int("Enter a whole number of minutes.")
    .min(1, `Must be between 1 and ${reconciliationLimits.maxWebhookStuckMinutes} minutes.`)
    .max(reconciliationLimits.maxWebhookStuckMinutes, `Must be between 1 and ${reconciliationLimits.maxWebhookStuckMinutes} minutes.`),
  refundPendingHours: z.coerce
    .number({ invalid_type_error: "Enter a number of hours." })
    .int("Enter a whole number of hours.")
    .min(1, `Must be between 1 and ${reconciliationLimits.maxRefundPendingHours} hours.`)
    .max(reconciliationLimits.maxRefundPendingHours, `Must be between 1 and ${reconciliationLimits.maxRefundPendingHours} hours.`),
});

export const jobScheduleSettingsSchema = z.object({
  reconciliationEnabled: z.boolean(),
  creditExpiryEnabled: z.boolean(),
  creditExpiryTime: marketTime,
  webhookCleanupEnabled: z.boolean(),
  webhookCleanupTime: marketTime,
});

export const errorAlertLimits = {
  maxThrottleMinutes: 7 * 24 * 60,
  maxCheckMinutes: 24 * 60,
  maxRetentionDays: 3650,
  emailMax: 254,
} as const;

const wholeBetween = (unit: string, max: number) =>
  z.coerce
    .number({ invalid_type_error: `Enter a number of ${unit}.` })
    .int(`Enter a whole number of ${unit}.`)
    .min(1, `Must be between 1 and ${max} ${unit}.`)
    .max(max, `Must be between 1 and ${max} ${unit}.`);

export const errorAlertSettingsSchema = z
  .object({
    emailAlertsEnabled: z.boolean(),
    errorAlertEmail: z
      .string()
      .trim()
      .max(errorAlertLimits.emailMax, "That email is too long.")
      .refine((value) => value === "" || z.string().email().safeParse(value).success, "Enter a valid email address."),
    errorAlertThrottleMinutes: wholeBetween("minutes", errorAlertLimits.maxThrottleMinutes),
    errorAlertCheckMinutes: wholeBetween("minutes", errorAlertLimits.maxCheckMinutes),
    errorEventRetentionDays: wholeBetween("days", errorAlertLimits.maxRetentionDays),
  })
  .refine((values) => !values.emailAlertsEnabled || values.errorAlertEmail !== "", {
    path: ["errorAlertEmail"],
    message: "Add the email that gets alerts before turning them on.",
  });

export const maxNotificationAttempts = 10;

export const notificationSettingsSchema = z.object({
  emailNotificationsEnabled: z.boolean(),
  notificationMaxAttempts: wholeBetween("tries", maxNotificationAttempts),
});

export const signInLimits = {
  maxSendsPerPhone: 20,
  maxWrongCodesPerPhone: 50,
  maxLockMinutes: 24 * 60,
} as const;

export const signInLimitSettingsSchema = z.object({
  otpMaxSendsPerPhone: wholeBetween("codes", signInLimits.maxSendsPerPhone),
  otpMaxWrongCodesPerPhone: wholeBetween("tries", signInLimits.maxWrongCodesPerPhone),
  otpLockMinutes: wholeBetween("minutes", signInLimits.maxLockMinutes),
});

export const maxWebhookPayloadRetentionDays = 3650;

/** The policy version may only go up, so the current value is the floor. */
export function privacySettingsSchema(currentPolicyVersion: number) {
  return z.object({
    policyVersion: z.coerce
      .number({ invalid_type_error: "Enter a version number." })
      .int("Enter a whole version number.")
      .min(currentPolicyVersion, `The version can only go up. It is ${currentPolicyVersion} now.`),
    webhookPayloadRetentionDays: z.coerce
      .number({ invalid_type_error: "Enter a number of days." })
      .int("Enter a whole number of days.")
      .min(1, `Must be between 1 and ${maxWebhookPayloadRetentionDays} days.`)
      .max(maxWebhookPayloadRetentionDays, `Must be between 1 and ${maxWebhookPayloadRetentionDays} days.`),
  });
}

export const resolveItemSchema = z.object({
  note: z
    .string()
    .trim()
    .min(1, "Say how this was resolved.")
    .max(reconciliationLimits.noteMax, `Keep the note to ${reconciliationLimits.noteMax} characters.`),
});

export const packLimits = {
  maxSessions: 100,
  maxPrice: 500_000,
  maxValidityDays: 730,
} as const;

const rupees = (max: number) =>
  z.coerce
    .number({ invalid_type_error: "Enter an amount." })
    .min(1, `Must be between ₹1 and ₹${max.toLocaleString("en-IN")}.`)
    .max(max, `Must be between ₹1 and ₹${max.toLocaleString("en-IN")}.`)
    .refine(twoDecimals, "Use at most 2 decimal places.");

const wholeNumber = (label: string, max: number) =>
  z.coerce
    .number({ invalid_type_error: `Enter the number of ${label}.` })
    .int(`Enter a whole number of ${label}.`)
    .min(1, `Must be between 1 and ${max} ${label}.`)
    .max(max, `Must be between 1 and ${max} ${label}.`);

export const packSchema = z.object({
  name: nameSchema,
  sessions: wholeNumber("sessions", packLimits.maxSessions),
  price: rupees(packLimits.maxPrice),
  validityDays: wholeNumber("days", packLimits.maxValidityDays),
  priceCap: rupees(packLimits.maxPrice),
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
export type CertificateRejectValues = z.infer<typeof certificateRejectSchema>;
export type AdminCancelValues = z.infer<typeof adminCancelSchema>;
export type UserBlockValues = z.infer<ReturnType<typeof userBlockSchema>>;
export type AreaFormValues = z.infer<typeof areaSchema>;
export type StateFormValues = z.infer<typeof stateSchema>;
export type StateEditValues = z.infer<typeof stateEditSchema>;
export type CityFormValues = z.infer<typeof citySchema>;
export type CityEditValues = z.infer<typeof cityEditSchema>;
export type AreaEditValues = z.infer<typeof areaEditSchema>;
export type RenameFormValues = z.infer<typeof renameSchema>;
export type FeeSettingsValues = z.infer<typeof feeSettingsSchema>;
export type BannerValues = z.infer<typeof bannerSchema>;
export type CreditSettingsValues = z.infer<typeof creditSettingsSchema>;
export type PackFormValues = z.infer<typeof packSchema>;
export type ReconciliationSettingsValues = z.infer<typeof reconciliationSettingsSchema>;
export type JobScheduleSettingsValues = z.infer<typeof jobScheduleSettingsSchema>;
export type ErrorAlertSettingsValues = z.infer<typeof errorAlertSettingsSchema>;
export type NotificationSettingsValues = z.infer<typeof notificationSettingsSchema>;
export type SignInLimitSettingsValues = z.infer<typeof signInLimitSettingsSchema>;
export type ResolveItemValues = z.infer<typeof resolveItemSchema>;
export type PrivacySettingsValues = z.infer<ReturnType<typeof privacySettingsSchema>>;

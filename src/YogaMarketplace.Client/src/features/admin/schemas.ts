import { z } from "zod";

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
    .min(1, "City is required.")
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

export const policySchema = z.object({
  platformFeePercent: z.coerce
    .number()
    .min(0, "Must be between 0 and 100.")
    .max(100, "Must be between 0 and 100."),
  cancelFreeWindowHours: z.coerce
    .number()
    .int("Enter a whole number of hours.")
    .min(0, "Must be between 0 and 168 hours.")
    .max(168, "Must be between 0 and 168 hours."),
  rescheduleFreeWindowHours: z.coerce
    .number()
    .int("Enter a whole number of hours.")
    .min(0, "Must be between 0 and 168 hours.")
    .max(168, "Must be between 0 and 168 hours."),
  lateCancelFeePercent: z.coerce
    .number()
    .min(0, "Must be between 0 and 100.")
    .max(100, "Must be between 0 and 100."),
  policyNote: z.string().trim().max(400, "Keep the note to 400 characters."),
});

export type RejectFormValues = z.infer<typeof rejectSchema>;
export type AdminCancelValues = z.infer<typeof adminCancelSchema>;
export type UserBlockValues = z.infer<ReturnType<typeof userBlockSchema>>;
export type AreaFormValues = z.infer<typeof areaSchema>;
export type AreaEditValues = z.infer<typeof areaEditSchema>;
export type RenameFormValues = z.infer<typeof renameSchema>;
export type PolicyFormValues = z.infer<typeof policySchema>;

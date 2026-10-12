import { z } from "zod";
import type { ProviderTrial, UpdateProviderTrialInput } from "@/features/instructor/types";

export const trialSchema = z
  .object({
    enabled: z.boolean(),
    price: z.string().trim(),
  })
  .superRefine((values, ctx) => {
    if (!values.enabled) return;
    if (!/^\d+(\.\d{1,2})?$/.test(values.price)) {
      ctx.addIssue({ code: "custom", path: ["price"], message: "Enter a price in rupees, for example 0 or 199." });
    }
  });

export type TrialFormValues = z.infer<typeof trialSchema>;

export function trialFormFromSettings(trial: ProviderTrial): TrialFormValues {
  return { enabled: trial.enabled, price: trial.price == null ? "" : String(trial.price) };
}

export function toTrialPayload(values: TrialFormValues): UpdateProviderTrialInput {
  return values.enabled ? { enabled: true, price: Number(values.price) } : { enabled: false };
}

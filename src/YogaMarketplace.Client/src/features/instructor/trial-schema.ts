import { z } from "zod";
import type { ProviderService, ProviderTrial, UpdateProviderTrialInput } from "@/features/instructor/types";

export const trialSchema = z
  .object({
    serviceId: z.string(),
    enabled: z.boolean(),
    price: z.string().trim(),
  })
  .superRefine((values, ctx) => {
    if (!values.enabled) return;
    if (!values.serviceId) {
      ctx.addIssue({ code: "custom", path: ["serviceId"], message: "Pick the service for the trial." });
    }
    if (!/^\d+(\.\d{1,2})?$/.test(values.price)) {
      ctx.addIssue({ code: "custom", path: ["price"], message: "Enter a price in rupees, for example 0 or 199." });
    }
  });

export type TrialFormValues = z.infer<typeof trialSchema>;

/** Starts on the service that carries the trial now, else the first active service. */
export function trialFormFromSettings(trial: ProviderTrial, services: ProviderService[]): TrialFormValues {
  const active = services.filter((service) => service.isActive);
  const serviceId =
    active.find((service) => service.id === trial.serviceId)?.id ?? active[0]?.id ?? "";
  return { serviceId, enabled: trial.enabled, price: trial.price == null ? "" : String(trial.price) };
}

export function toTrialPayload(values: TrialFormValues): UpdateProviderTrialInput {
  if (!values.enabled) return { enabled: false };
  return { serviceId: values.serviceId || undefined, enabled: true, price: Number(values.price) };
}

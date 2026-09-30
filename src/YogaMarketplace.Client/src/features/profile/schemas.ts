import { z } from "zod";
import type { UpdateCustomerAccountInput } from "@/features/profile/types";

export const customerAccountSchema = z
  .object({
    name: z
      .string()
      .trim()
      .min(2, "Name must be 2 to 80 characters.")
      .max(80, "Name must be 2 to 80 characters."),
    gender: z.string(),
  })
  .superRefine((values, ctx) => {
    if (values.gender !== "Female" && values.gender !== "Male" && values.gender !== "Other") {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["gender"],
        message: "Gender is required. Use Female, Male, or Other.",
      });
    }
  });

export type CustomerAccountFormValues = z.infer<typeof customerAccountSchema>;

export function toAccountPayload(values: CustomerAccountFormValues): UpdateCustomerAccountInput {
  return {
    name: values.name.trim(),
    gender: values.gender,
  };
}

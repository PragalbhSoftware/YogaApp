import { z } from "zod";

function isIndianMobile(value: string) {
  const digits = value.replace(/\D/g, "");
  const withCountry = digits.length === 10 ? `91${digits}` : digits;
  return (
    withCountry.length === 12 &&
    withCountry.startsWith("91") &&
    withCountry[2] >= "6" &&
    withCountry[2] <= "9"
  );
}

export const phoneSchema = z
  .string()
  .trim()
  .min(1, "Phone is required.")
  .refine(isIndianMobile, "Enter a valid Indian mobile number.");

export const accountSchema = z
  .object({
    accountKind: z.enum(["existing", "new"]),
    phone: phoneSchema,
    name: z.string().trim().optional(),
    gender: z.string().optional(),
  })
  .superRefine((value, ctx) => {
    if (value.accountKind !== "new") return;
    if (!value.name || value.name.length < 2) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["name"],
        message: "Name is required for a new account.",
      });
    }
    if (!value.gender || !["Female", "Male", "Other"].includes(value.gender)) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["gender"],
        message: "Gender is required for a new account. Use Female, Male, or Other.",
      });
    }
  });

export const otpSchema = z.object({
  code: z.string().trim().length(6, "Enter the 6-digit code we sent."),
});

export type AccountFormValues = z.infer<typeof accountSchema>;
export type OtpFormValues = z.infer<typeof otpSchema>;

import { z } from "zod";

export const homeVisitFormSchema = z
  .object({
    line1: z.string().trim().min(4, "Enter house and street.").max(160, "House and street is too long."),
    area: z.string().trim().min(2, "Enter the area or locality.").max(80, "Area is too long."),
    city: z.string().trim().min(2, "Enter the city.").max(60, "City is too long."),
    pin: z.string().trim().regex(/^[1-9][0-9]{5}$/, "Enter a 6-digit PIN code."),
    landmark: z.string().trim().min(2, "Add a nearby landmark.").max(160, "Landmark is too long."),
  })
  .superRefine((values, ctx) => {
    if (composeHomeAddress(values).length > 300) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["line1"],
        message: "Address is too long.",
      });
    }
  });

export type HomeVisitFormValues = z.infer<typeof homeVisitFormSchema>;

export type HomeVisitValues = {
  homeAddress: string;
  landmark: string;
};

export function composeHomeAddress(values: Pick<HomeVisitFormValues, "line1" | "area" | "city" | "pin">) {
  return `${values.line1}, ${values.area}, ${values.city} ${values.pin}`;
}

export const emptyHomeVisitForm: HomeVisitFormValues = {
  line1: "",
  area: "",
  city: "",
  pin: "",
  landmark: "",
};

export function toHomeVisitPayload(values: HomeVisitFormValues): HomeVisitValues {
  return {
    homeAddress: composeHomeAddress(values),
    landmark: values.landmark,
  };
}

export function toHomeVisitFormValues(address: HomeVisitFormValues): HomeVisitFormValues {
  return {
    line1: address.line1,
    area: address.area,
    city: address.city,
    pin: address.pin,
    landmark: address.landmark,
  };
}

export const reviewFormSchema = z.object({
  rating: z
    .union([z.number(), z.null()])
    .refine((value): value is number => value != null && Number.isInteger(value) && value >= 1 && value <= 5, {
      message: "Choose a rating from 1 to 5.",
    }),
  comment: z.string().trim().max(1000, "Keep the review under 1000 characters."),
});

export type ReviewFormInput = z.input<typeof reviewFormSchema>;
export type ReviewFormValues = z.output<typeof reviewFormSchema>;

export const emptyReviewForm: ReviewFormInput = {
  rating: null,
  comment: "",
};

export function toReviewPayload(values: ReviewFormValues): { rating: number; comment?: string } {
  const comment = values.comment.trim();
  return comment ? { rating: values.rating, comment } : { rating: values.rating };
}

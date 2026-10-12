import { z } from "zod";
import { contentPageLimits } from "@/features/content/types";

export const contentPageSchema = z.object({
  title: z
    .string()
    .trim()
    .min(2, "Enter a title of at least 2 characters.")
    .max(contentPageLimits.titleMax, `Keep the title under ${contentPageLimits.titleMax} characters.`),
  body: z
    .string()
    .trim()
    .min(1, "Enter the page text.")
    .max(contentPageLimits.bodyMax, `Keep the text under ${contentPageLimits.bodyMax.toLocaleString("en-IN")} characters.`),
});

export type ContentPageValues = z.infer<typeof contentPageSchema>;

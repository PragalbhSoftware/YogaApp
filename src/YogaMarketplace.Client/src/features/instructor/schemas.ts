import { z } from "zod";
import type {
  InstructorProfile,
  RegisterInstructorInput,
  UpdateInstructorRatesInput,
} from "@/features/instructor/types";

function parseOptionalInt(value: string) {
  const trimmed = value.trim();
  if (!trimmed) return null;
  if (!/^\d+$/.test(trimmed)) return Number.NaN;
  return Number(trimmed);
}

function parseOptionalRate(value: string) {
  const trimmed = value.trim();
  if (!trimmed) return null;
  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : Number.NaN;
}

function isGoogleMeetHttps(value: string) {
  try {
    const uri = new URL(value);
    return uri.protocol === "https:" && uri.hostname.toLowerCase() === "meet.google.com";
  } catch {
    return false;
  }
}

export const instructorRegisterSchema = z
  .object({
    displayName: z
      .string()
      .trim()
      .min(2, "Display name must be 2 to 80 characters.")
      .max(80, "Display name must be 2 to 80 characters."),
    age: z.string(),
    email: z.string(),
    areaId: z.string().min(1, "Choose an area."),
    bio: z.string().max(1000, "Bio must be 1000 characters or less."),
    offersHome: z.boolean(),
    offersStudio: z.boolean(),
    offersOnline: z.boolean(),
    homeRate: z.string(),
    studioRate: z.string(),
    studioAddress: z.string(),
    onlineRate: z.string(),
    googleMeetLink: z.string(),
  })
  .superRefine((values, ctx) => {
    if (!values.offersHome && !values.offersStudio && !values.offersOnline) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["offersHome"],
        message: "Choose at least one session mode: Home, Studio, or Online.",
      });
    }

    const age = parseOptionalInt(values.age);
    if (values.age.trim() && (age == null || Number.isNaN(age) || age < 18 || age > 80)) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["age"],
        message: "Age must be between 18 and 80.",
      });
    }

    const email = values.email.trim();
    if (email && (email.length > 200 || !email.includes("@") || email.startsWith("@") || email.endsWith("@"))) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["email"],
        message: "Enter a valid email.",
      });
    }

    if (values.offersHome) {
      const rate = parseOptionalRate(values.homeRate);
      if (rate == null || Number.isNaN(rate) || rate <= 0 || rate > 100000) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["homeRate"],
          message: "Home sessions need a rate in INR.",
        });
      }
    }

    if (values.offersStudio) {
      const rate = parseOptionalRate(values.studioRate);
      if (rate == null || Number.isNaN(rate) || rate <= 0 || rate > 100000) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["studioRate"],
          message: "Studio sessions need a rate in INR.",
        });
      }
      const address = values.studioAddress.trim();
      if (!address) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["studioAddress"],
          message: "Studio sessions need the studio address.",
        });
      } else if (address.length > 300) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["studioAddress"],
          message: "Studio address must be 300 characters or less.",
        });
      }
    }

    if (values.offersOnline) {
      const rate = parseOptionalRate(values.onlineRate);
      if (rate == null || Number.isNaN(rate) || rate <= 0 || rate > 100000) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["onlineRate"],
          message: "Online sessions need a rate in INR.",
        });
      }
      if (!isGoogleMeetHttps(values.googleMeetLink.trim())) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["googleMeetLink"],
          message: "Online sessions need an https://meet.google.com link.",
        });
      }
    }
  });

export type InstructorRegisterFormValues = z.infer<typeof instructorRegisterSchema>;

export function emptyInstructorRegisterForm(displayName = ""): InstructorRegisterFormValues {
  return {
    displayName,
    age: "",
    email: "",
    areaId: "",
    bio: "",
    offersHome: false,
    offersStudio: false,
    offersOnline: false,
    homeRate: "",
    studioRate: "",
    studioAddress: "",
    onlineRate: "",
    googleMeetLink: "",
  };
}

export function toRegisterPayload(values: InstructorRegisterFormValues): RegisterInstructorInput {
  const age = parseOptionalInt(values.age);
  const email = values.email.trim();
  const bio = values.bio.trim();
  const payload: RegisterInstructorInput = {
    displayName: values.displayName.trim(),
    areaId: values.areaId,
    offersHome: values.offersHome,
    offersStudio: values.offersStudio,
    offersOnline: values.offersOnline,
  };
  if (age != null && !Number.isNaN(age)) payload.age = age;
  if (email) payload.email = email;
  if (bio) payload.bio = bio;
  if (values.offersHome) payload.homeRate = parseOptionalRate(values.homeRate) ?? undefined;
  if (values.offersStudio) {
    payload.studioRate = parseOptionalRate(values.studioRate) ?? undefined;
    payload.studioAddress = values.studioAddress.trim();
  }
  if (values.offersOnline) {
    payload.onlineRate = parseOptionalRate(values.onlineRate) ?? undefined;
    payload.googleMeetLink = values.googleMeetLink.trim();
  }
  return payload;
}

export const instructorRatesSchema = (profile: InstructorProfile) =>
  z
    .object({
      homeRate: z.string(),
      studioRate: z.string(),
      onlineRate: z.string(),
    })
    .superRefine((values, ctx) => {
      if (profile.offersHome) {
        const rate = parseOptionalRate(values.homeRate);
        if (rate == null || Number.isNaN(rate) || rate <= 0 || rate > 100000) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            path: ["homeRate"],
            message: "Home sessions need a rate in INR.",
          });
        }
      }

      if (profile.offersStudio) {
        const rate = parseOptionalRate(values.studioRate);
        if (rate == null || Number.isNaN(rate) || rate <= 0 || rate > 100000) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            path: ["studioRate"],
            message: "Studio sessions need a rate in INR.",
          });
        }
      }

      if (profile.offersOnline) {
        const rate = parseOptionalRate(values.onlineRate);
        if (rate == null || Number.isNaN(rate) || rate <= 0 || rate > 100000) {
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            path: ["onlineRate"],
            message: "Online sessions need a rate in INR.",
          });
        }
      }
    });

export type InstructorRatesFormValues = z.infer<ReturnType<typeof instructorRatesSchema>>;

function rateToInput(value: number | null) {
  if (value == null || Number.isNaN(value)) return "";
  return String(value);
}

export function ratesFormFromProfile(profile: InstructorProfile): InstructorRatesFormValues {
  return {
    homeRate: rateToInput(profile.homeRate),
    studioRate: rateToInput(profile.studioRate),
    onlineRate: rateToInput(profile.onlineRate),
  };
}

export function profileFormFromInstructor(profile: InstructorProfile): InstructorRegisterFormValues {
  return {
    displayName: profile.displayName,
    age: profile.age == null ? "" : String(profile.age),
    email: profile.email ?? "",
    areaId: profile.areaId,
    bio: profile.bio ?? "",
    offersHome: profile.offersHome,
    offersStudio: profile.offersStudio,
    offersOnline: profile.offersOnline,
    homeRate: rateToInput(profile.homeRate),
    studioRate: rateToInput(profile.studioRate),
    studioAddress: profile.studioAddress ?? "",
    onlineRate: rateToInput(profile.onlineRate),
    googleMeetLink: profile.googleMeetLink ?? "",
  };
}

export function toRatesPayload(
  profile: InstructorProfile,
  values: InstructorRatesFormValues,
): UpdateInstructorRatesInput {
  const payload: UpdateInstructorRatesInput = {};
  if (profile.offersHome) payload.homeRate = parseOptionalRate(values.homeRate) ?? undefined;
  if (profile.offersStudio) payload.studioRate = parseOptionalRate(values.studioRate) ?? undefined;
  if (profile.offersOnline) payload.onlineRate = parseOptionalRate(values.onlineRate) ?? undefined;
  return payload;
}

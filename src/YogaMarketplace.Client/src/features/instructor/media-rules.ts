import { z } from "zod";

const MB = 1024 * 1024;

/** Mirrors the API limits so customers get instant feedback; the API still checks the file bytes. */
export const photoRules = {
  max: 6,
  maxBytes: 5 * MB,
  accept: "image/jpeg,image/png,image/webp",
  types: ["image/jpeg", "image/png", "image/webp"],
  typeMessage: "Photos must be JPEG, PNG or WebP.",
} as const;

export const certificateRules = {
  max: 5,
  maxBytes: 10 * MB,
  accept: "application/pdf,image/jpeg,image/png",
  types: ["application/pdf", "image/jpeg", "image/png"],
  typeMessage: "Certificates must be PDF, JPEG or PNG.",
} as const;

type FileRules = typeof photoRules | typeof certificateRules;

/** Returns a message when the file is clearly not allowed, otherwise null. */
export function fileProblem(file: File, rules: FileRules): string | null {
  if (file.size === 0) return "The file is empty.";
  if (file.size > rules.maxBytes) return `The file must be ${rules.maxBytes / MB} MB or smaller.`;
  if (file.type && !(rules.types as readonly string[]).includes(file.type)) return rules.typeMessage;
  return null;
}

export const introVideoSchema = z.object({
  url: z
    .string()
    .trim()
    .max(300, "That link is too long.")
    .refine(
      (value) => value === "" || /^https?:\/\/([a-z0-9-]+\.)?(youtube\.com|youtu\.be|youtube-nocookie\.com)\//i.test(value),
      "Paste a YouTube link.",
    ),
});

export const certificateSchema = z.object({
  title: z.string().trim().min(2, "Give the certificate a title.").max(120, "Keep the title to 120 characters."),
});

export type IntroVideoValues = z.infer<typeof introVideoSchema>;
export type CertificateFormValues = z.infer<typeof certificateSchema>;

export function youTubeWatchUrl(videoId: string | null) {
  return videoId ? `https://www.youtube.com/watch?v=${videoId}` : "";
}

const extensions: Record<string, string> = {
  "application/pdf": ".pdf",
  "image/jpeg": ".jpg",
  "image/png": ".png",
  "image/webp": ".webp",
};

/** File name for a downloaded certificate, built from its title and type. */
export function certificateFileName(title: string, contentType: string) {
  const safe = title.replace(/[^\w\s.-]/g, "").trim() || "certificate";
  return `${safe}${extensions[contentType] ?? ""}`;
}

export function formatFileSize(bytes: number) {
  return bytes >= MB ? `${(bytes / MB).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

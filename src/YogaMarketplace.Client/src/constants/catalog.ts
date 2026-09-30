export const categorySlug = "yoga";

export const sessionModes = ["Home", "Studio", "Online"] as const;

export type SessionMode = (typeof sessionModes)[number];

export type SessionModeFilter = SessionMode | "";

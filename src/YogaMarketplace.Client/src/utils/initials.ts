export function initials(name: string | null | undefined) {
  if (!name?.trim()) return "";
  const parts = name.trim().split(/\s+/);
  const first = parts[0]?.[0];
  const last = parts.length > 1 ? parts[parts.length - 1]?.[0] : undefined;
  return `${first ?? ""}${last ?? ""}`.toUpperCase();
}

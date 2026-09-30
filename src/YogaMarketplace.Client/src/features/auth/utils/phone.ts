export function digitsOnly(value: string) {
  let digits = value.replace(/\D/g, "");
  if (digits.startsWith("91") && digits.length > 10) digits = digits.slice(2);
  return digits.slice(0, 10);
}

export function formatMobileDisplay(value: string) {
  const digits = digitsOnly(value);
  if (digits.length <= 5) return digits;
  return `${digits.slice(0, 5)} ${digits.slice(5)}`;
}

export function formatPhoneLine(value: string | null | undefined) {
  const grouped = formatMobileDisplay(value ?? "");
  return grouped ? `+91 ${grouped}` : "";
}

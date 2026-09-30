import { formatPhoneLine } from "@/features/auth/utils/phone";
import { cn } from "@/utils/cn";

type PhoneTextProps = {
  value: string | null | undefined;
  className?: string;
};

export function PhoneText({ value, className }: PhoneTextProps) {
  const formatted = formatPhoneLine(value);
  if (!formatted) return null;

  return (
    <span className={cn("inline-block whitespace-nowrap tabular-nums", className)}>
      {formatted}
    </span>
  );
}

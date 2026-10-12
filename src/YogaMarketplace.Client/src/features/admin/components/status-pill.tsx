import { statusTone, type StatusTone } from "@/features/admin/list-utils";
import { cn } from "@/utils/cn";

const toneClasses: Record<StatusTone, string> = {
  neutral: "bg-brand-background text-brand-muted",
  positive: "bg-brand-primary/10 text-brand-primary",
  attention: "bg-brand-secondary/20 text-brand-text",
  negative: "bg-brand-accent/15 text-brand-text",
};

type StatusPillProps = {
  status: string;
  label?: string;
  className?: string;
};

export function StatusPill({ status, label, className }: StatusPillProps) {
  return (
    <span
      className={cn(
        "inline-flex shrink-0 items-center rounded-full px-2.5 py-1 text-xs font-semibold whitespace-nowrap",
        toneClasses[statusTone(status)],
        className,
      )}
    >
      {label ?? status}
    </span>
  );
}

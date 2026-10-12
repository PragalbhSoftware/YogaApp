import { Link } from "react-router-dom";
import { ChevronRight } from "lucide-react";
import { cn } from "@/utils/cn";

type QueueCardProps = {
  to: string;
  label: string;
  value: number;
  hint: string;
};

/** One row of work waiting. A non-zero count is highlighted so it stands out at a glance. */
export function QueueCard({ to, label, value, hint }: QueueCardProps) {
  const waiting = value > 0;
  return (
    <Link
      to={to}
      className="group flex items-center gap-4 rounded-2xl px-4 py-3.5 transition-colors hover:bg-brand-background focus-visible:bg-brand-background"
    >
      <span
        className={cn(
          "inline-flex min-w-11 justify-center rounded-xl px-2 py-1.5 font-heading text-lg font-medium tabular-nums",
          waiting ? "bg-brand-secondary/20 text-brand-text" : "bg-brand-background text-brand-muted",
        )}
      >
        {value}
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-sm font-medium text-brand-text">{label}</span>
        <span className="block text-xs text-brand-muted">{hint}</span>
      </span>
      <ChevronRight className="size-4 shrink-0 text-brand-muted group-hover:text-brand-primary" aria-hidden="true" />
    </Link>
  );
}

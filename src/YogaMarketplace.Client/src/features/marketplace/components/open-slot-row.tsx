import { Button } from "@mui/material";
import { Link } from "react-router-dom";
import { Clock } from "lucide-react";
import { bookPath } from "@/constants/routes";
import type { OpenSlot } from "@/features/marketplace/types";
import { durationLabel, formatTime12, slotHasEnded } from "@/utils/clock";
import { cn } from "@/utils/cn";

type OpenSlotRowProps = {
  slot: OpenSlot;
  providerId: string;
};

export function OpenSlotRow({ slot, providerId }: OpenSlotRowProps) {
  const ended = slotHasEnded(slot.date, slot.end);
  const duration = durationLabel(slot.start, slot.end);

  return (
    <article
      className={cn(
        "flex items-center gap-4 rounded-[24px] border border-brand-border bg-brand-surface px-4 py-4 shadow-[0_8px_24px_rgba(37,49,39,0.04)] sm:px-5",
        ended && "opacity-70",
      )}
    >
      <span
        className={cn(
          "flex size-12 shrink-0 items-center justify-center rounded-2xl",
          ended ? "bg-brand-background text-brand-muted" : "bg-[rgba(101,122,91,0.12)] text-brand-primary",
        )}
        aria-hidden="true"
      >
        <Clock className="size-5" />
      </span>
      <div className="min-w-0 flex-1">
        <p className="font-heading text-xl font-medium tracking-tight">{formatTime12(slot.start)}</p>
        <p className="mt-0.5 text-sm text-brand-muted">
          to {formatTime12(slot.end)}
          {duration ? ` · ${duration}` : ""}
          {ended ? " · Ended" : ""}
        </p>
      </div>
      {ended ? null : (
        <Button
          component={Link}
          to={bookPath(providerId, slot.id, slot.mode)}
          variant="contained"
          size="small"
          sx={{ flexShrink: 0, minHeight: 44, minWidth: 88, borderRadius: "16px", px: 2.5 }}
        >
          Book
        </Button>
      )}
    </article>
  );
}

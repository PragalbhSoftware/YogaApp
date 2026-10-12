import { Button } from "@mui/material";
import type { Pack } from "@/features/credits/types";
import { cn } from "@/utils/cn";
import { formatInr } from "@/utils/money";

type PackCardProps = {
  pack: Pack;
  highlighted: boolean;
  busy: boolean;
  buying: boolean;
  onBuy: (pack: Pack) => void;
};

export function PackCard({ pack, highlighted, busy, buying, onBuy }: PackCardProps) {
  return (
    <article
      id={`pack-${pack.id}`}
      className={cn(
        "flex flex-col gap-4 rounded-3xl border bg-brand-surface p-5 shadow-[0_8px_24px_rgba(37,49,39,0.05)]",
        highlighted ? "border-brand-primary" : "border-brand-border",
      )}
    >
      <div>
        <h2 className="font-heading text-lg font-medium">{pack.name}</h2>
        <p className="text-sm text-brand-muted">
          {pack.sessions} {pack.sessions === 1 ? "session" : "sessions"} · use within {pack.validityDays} days
        </p>
      </div>
      <p className="flex items-baseline gap-2">
        <span className="font-heading text-3xl font-medium tabular-nums">{formatInr(pack.price)}</span>
        <span className="text-sm text-brand-muted">{formatInr(pack.perSession)} a session</span>
      </p>
      <p className="text-sm leading-relaxed text-brand-muted">
        Any instructor, any mode, for sessions priced up to {formatInr(pack.priceCap)}. Above that you pay as usual.
      </p>
      <Button
        variant={highlighted ? "contained" : "outlined"}
        fullWidth
        disabled={busy}
        onClick={() => onBuy(pack)}
        sx={{ minHeight: 48, borderRadius: "14px", mt: "auto" }}
      >
        {buying ? "Opening payment…" : `Buy for ${formatInr(pack.price)}`}
      </Button>
    </article>
  );
}

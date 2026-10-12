import type { ReactNode } from "react";

type MediaHeadingProps = {
  title: string;
  caption: string;
  count?: { used: number; max: number };
  /** `card` is the main heading of a card; `item` a heading inside one. */
  level?: "card" | "item";
  badge?: ReactNode;
};

export function MediaHeading({ title, caption, count, level = "item", badge }: MediaHeadingProps) {
  const Heading = level === "card" ? "h2" : "h3";
  return (
    <div className="space-y-0.5">
      <div className="flex items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <Heading className={`font-heading font-medium ${level === "card" ? "text-lg" : "text-base"}`}>
            {title}
          </Heading>
          {badge}
        </div>
        {count ? (
          <span className="text-xs font-medium text-brand-muted tabular-nums">
            {count.used} of {count.max}
          </span>
        ) : null}
      </div>
      <p className={`leading-relaxed text-brand-muted ${level === "card" ? "text-sm" : "text-xs"}`}>{caption}</p>
    </div>
  );
}

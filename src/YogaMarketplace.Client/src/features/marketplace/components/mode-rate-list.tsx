import { PriceText } from "@/components/common/price-text";
import type { ModeRate } from "@/features/marketplace/types";

type ModeRateListProps = {
  modes: ModeRate[];
  heading?: string;
};

export function ModeRateList({ modes, heading }: ModeRateListProps) {
  if (modes.length === 0) return null;

  return (
    <section>
      {heading ? (
        <h2 className="mb-2 font-heading text-lg font-medium text-brand-text">{heading}</h2>
      ) : null}
      <ul
        className="grid divide-x divide-brand-border overflow-hidden rounded-2xl border border-brand-border bg-brand-surface"
        style={{ gridTemplateColumns: `repeat(${modes.length}, minmax(0, 1fr))` }}
      >
        {modes.map((item) => (
          <li key={item.mode} className="flex flex-col items-center px-2 py-3">
            <span className="text-[11px] font-medium tracking-wide text-brand-muted uppercase">
              {item.mode}
            </span>
            <PriceText amount={Number(item.rate)} className="mt-1 text-brand-text" />
          </li>
        ))}
      </ul>
    </section>
  );
}

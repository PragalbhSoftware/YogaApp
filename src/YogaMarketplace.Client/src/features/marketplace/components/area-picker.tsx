import { useMemo, useState } from "react";
import { Button, Chip } from "@mui/material";
import { MapPin } from "lucide-react";
import { AreaPickerSkeleton } from "@/components/common/loading-skeleton";
import { EmptyState } from "@/components/common/empty-state";
import { ErrorState } from "@/components/common/error-state";
import { useAreas } from "@/features/marketplace/hooks/use-areas";
import type { Area } from "@/features/marketplace/types";
import { toUserMessage } from "@/services/http/api-error";
import { cn } from "@/utils/cn";

type AreaPickerProps = {
  selectedCity?: string | null;
  selectedName?: string | null;
  onSelect: (city: string, name: string) => void;
  onCancel?: () => void;
};

function groupByCity(areas: Area[]) {
  const groups = new Map<string, Area[]>();
  for (const area of areas) {
    groups.set(area.city, [...(groups.get(area.city) ?? []), area]);
  }
  return groups;
}

export function AreaPicker({ selectedCity, selectedName, onSelect, onCancel }: AreaPickerProps) {
  const areas = useAreas();
  const groups = useMemo(() => groupByCity(areas.data ?? []), [areas.data]);
  const cities = [...groups.keys()];
  const [pickedCity, setPickedCity] = useState<string | null>(selectedCity ?? null);
  const city = cities.length === 1 ? cities[0] : pickedCity && groups.has(pickedCity) ? pickedCity : null;
  const neighbourhoods = city ? (groups.get(city) ?? []) : [];

  return (
    <section className="mx-auto w-full max-w-3xl px-4 py-6 sm:px-8">
      <header className="flex flex-col gap-2">
        <p className="text-[11px] font-medium tracking-[0.22em] text-brand-muted uppercase">
          Your area
        </p>
        <h1 className="font-heading text-2xl font-medium sm:text-3xl">
          {city ? `Choose your neighbourhood in ${city}` : "Choose your city"}
        </h1>
        <p className="text-sm leading-relaxed text-brand-muted">
          We’ll show verified instructors near you. You can change this anytime.
        </p>
      </header>

      <div className="mt-6 flex flex-col gap-5">
        {areas.isLoading ? <AreaPickerSkeleton /> : null}
        {areas.isError ? (
          <ErrorState message={toUserMessage(areas.error)} onRetry={() => void areas.refetch()} />
        ) : null}
        {areas.isSuccess && areas.data.length === 0 ? (
          <EmptyState
            title="No neighbourhoods yet"
            description="Areas will appear here once the marketplace lists them."
          />
        ) : null}

        {cities.length > 1 ? (
          <div role="group" aria-label="City" className="flex flex-wrap gap-2">
            {cities.map((name) => (
              <Chip
                key={name}
                label={name}
                clickable
                color={name === city ? "primary" : "default"}
                variant={name === city ? "filled" : "outlined"}
                aria-pressed={name === city}
                onClick={() => setPickedCity(name)}
                sx={{ minHeight: 40, px: 1, borderRadius: "999px", fontWeight: 600 }}
              />
            ))}
          </div>
        ) : null}

        {neighbourhoods.length > 0 ? (
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            {neighbourhoods.map((area) => {
              const selected = area.name === selectedName && area.city === selectedCity;
              return (
                <button
                  key={area.id}
                  type="button"
                  onClick={() => onSelect(area.city, area.name)}
                  aria-pressed={selected}
                  className={cn(
                    "flex items-center gap-3 rounded-2xl border bg-brand-surface px-4 py-4 text-left shadow-[0_8px_24px_rgba(37,49,39,0.04)]",
                    selected
                      ? "border-brand-primary ring-2 ring-brand-primary/20"
                      : "border-brand-border",
                  )}
                >
                  <span className="flex size-10 items-center justify-center rounded-full bg-brand-primary/10 text-brand-primary">
                    <MapPin className="size-4" aria-hidden="true" />
                  </span>
                  <span className="min-w-0 font-medium">{area.name}</span>
                </button>
              );
            })}
          </div>
        ) : null}
      </div>

      {onCancel ? (
        <div className="mt-6">
          <Button onClick={onCancel} variant="text">
            Keep {selectedName}
          </Button>
        </div>
      ) : null}
    </section>
  );
}

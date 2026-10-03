import { Chip } from "@mui/material";
import { MapPin } from "lucide-react";
import { launch } from "@/constants/launch";
import { site } from "@/constants/site";

type HomeHeroProps = {
  city: string;
  areaName: string;
  onChangeArea: () => void;
};

export function HomeHero({ city, areaName, onChangeArea }: HomeHeroProps) {
  const place = `${areaName}, ${city}`;

  return (
    <header className="relative overflow-hidden bg-brand-primary px-5 py-6 text-white sm:px-8">
      <div className="pointer-events-none absolute inset-0" aria-hidden="true">
        <div className="absolute -top-12 -left-8 size-40 rounded-full bg-white/10" />
        <div className="absolute right-[-28px] bottom-[-40px] size-44 rounded-full border border-white/15" />
      </div>
      <div className="relative z-10 mx-auto flex max-w-3xl flex-col items-start gap-3">
        <p className="text-[11px] font-medium tracking-[0.22em] uppercase opacity-80">
          Yoga Marketplace
        </p>
        <h1 className="font-heading text-2xl leading-tight font-medium sm:text-3xl">
          {site.tagline}
        </h1>
        <div className="flex flex-col items-start gap-2">
          <button
            type="button"
            onClick={onChangeArea}
            className="inline-flex max-w-full items-center gap-2 rounded-full border border-white/25 bg-white/10 px-3 py-1.5 text-sm"
            aria-label={`Change area. Currently ${place}`}
          >
            <MapPin className="size-3.5 shrink-0" aria-hidden="true" />
            <span className="truncate">{place}</span>
            <span className="text-xs opacity-80">Change</span>
          </button>
          <Chip
            label={launch.heroChip}
            size="small"
            sx={{
              bgcolor: "rgba(255,255,255,0.12)",
              color: "white",
              border: "1px solid rgba(255,255,255,0.25)",
              width: "fit-content",
            }}
          />
        </div>
      </div>
    </header>
  );
}

import { MapPin, ShieldCheck } from "lucide-react";
import { Chip } from "@mui/material";
import { launch } from "@/constants/launch";

const highlights = [
  { icon: ShieldCheck, label: "Verified instructors" },
  { icon: MapPin, label: launch.locationHighlight },
];

export function LoginHero() {
  return (
    <aside className="relative overflow-hidden bg-brand-primary px-5 py-7 text-white sm:px-8 sm:py-8 md:px-10">
      <div className="pointer-events-none absolute inset-0" aria-hidden="true">
        <div className="absolute -top-16 -left-10 size-48 rounded-full bg-white/10" />
        <div className="absolute right-[-40px] bottom-[-56px] size-56 rounded-full border border-white/15" />
      </div>

      <div className="relative z-10 mx-auto max-w-md space-y-4">
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
        <div className="space-y-2">
          <p className="text-[11px] font-medium tracking-[0.22em] uppercase opacity-80">
            Yoga Marketplace
          </p>
          <h1 className="font-heading text-[1.75rem] leading-tight font-medium sm:text-3xl">
            Book verified yoga instructors — home, studio, or online.
          </h1>
        </div>
        <div className="flex flex-wrap gap-2">
          {highlights.map((item) => (
            <span
              key={item.label}
              className="inline-flex items-center gap-1.5 rounded-full border border-white/20 bg-white/10 px-3 py-1.5 text-xs"
            >
              <item.icon className="size-3.5 shrink-0" aria-hidden="true" />
              {item.label}
            </span>
          ))}
        </div>
      </div>
    </aside>
  );
}

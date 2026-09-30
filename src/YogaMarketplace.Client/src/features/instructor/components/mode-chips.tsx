import { Chip } from "@mui/material";
import type { SessionMode } from "@/constants/catalog";

type ModeChipsProps = {
  value: SessionMode;
  options: SessionMode[];
  onChange: (mode: SessionMode) => void;
};

export function ModeChips({ value, options, onChange }: ModeChipsProps) {
  return (
    <div className="flex flex-wrap gap-2" role="group" aria-label="Session type">
      {options.map((mode) => {
        const selected = mode === value;
        return (
          <Chip
            key={mode}
            label={mode}
            clickable
            onClick={() => onChange(mode)}
            aria-pressed={selected}
            sx={{
              bgcolor: selected ? "primary.main" : "background.paper",
              color: selected ? "primary.contrastText" : "text.primary",
              border: "1px solid",
              borderColor: selected ? "primary.main" : "divider",
              fontWeight: 600,
              "&:hover": {
                bgcolor: selected ? "primary.main" : "background.paper",
              },
            }}
          />
        );
      })}
    </div>
  );
}

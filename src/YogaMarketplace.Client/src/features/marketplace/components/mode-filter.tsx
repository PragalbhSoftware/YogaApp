import { Chip } from "@mui/material";
import { sessionModes, type SessionModeFilter } from "@/constants/catalog";

type ModeFilterProps = {
  value: SessionModeFilter;
  onChange: (mode: SessionModeFilter) => void;
};

const options: { value: SessionModeFilter; label: string }[] = [
  { value: "", label: "Any mode" },
  ...sessionModes.map((mode) => ({ value: mode, label: mode })),
];

export function ModeFilter({ value, onChange }: ModeFilterProps) {
  return (
    <div className="flex flex-wrap gap-2" role="group" aria-label="Session type">
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <Chip
            key={option.label}
            label={option.label}
            clickable
            onClick={() => onChange(option.value)}
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

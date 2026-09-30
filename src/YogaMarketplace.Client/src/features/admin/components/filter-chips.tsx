import { Chip } from "@mui/material";

export type FilterOption = {
  value: string | null;
  label: string;
};

type FilterChipsProps = {
  value: string | null;
  options: FilterOption[];
  onChange: (value: string | null) => void;
  label: string;
};

export function FilterChips({ value, options, onChange, label }: FilterChipsProps) {
  return (
    <div className="flex flex-wrap gap-2" role="group" aria-label={label}>
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <Chip
            key={option.value ?? "all"}
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

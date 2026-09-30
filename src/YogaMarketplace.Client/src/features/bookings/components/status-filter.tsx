import { Chip } from "@mui/material";
import { bookingStatuses } from "@/features/bookings/types";
import { statusFilterLabel } from "@/features/bookings/utils/status";

type StatusFilterProps = {
  value: string | null;
  onChange: (status: string | null) => void;
};

export function StatusFilter({ value, onChange }: StatusFilterProps) {
  return (
    <div className="flex flex-wrap gap-2" role="group" aria-label="Booking status">
      <Chip
        label="All"
        clickable
        onClick={() => onChange(null)}
        aria-pressed={value === null}
        sx={chipSx(value === null)}
      />
      {bookingStatuses.map((status) => {
        const selected = value === status;
        return (
          <Chip
            key={status}
            label={statusFilterLabel(status)}
            clickable
            onClick={() => onChange(status)}
            aria-pressed={selected}
            sx={chipSx(selected)}
          />
        );
      })}
    </div>
  );
}

function chipSx(selected: boolean) {
  return {
    bgcolor: selected ? "primary.main" : "background.paper",
    color: selected ? "primary.contrastText" : "text.primary",
    border: "1px solid",
    borderColor: selected ? "primary.main" : "divider",
    fontWeight: 600,
    "&:hover": {
      bgcolor: selected ? "primary.main" : "background.paper",
    },
  };
}

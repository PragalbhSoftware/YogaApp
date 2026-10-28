import { useState } from "react";
import { Button } from "@mui/material";
import { MapPin } from "lucide-react";
import { CityPickerDialog } from "@/features/marketplace/components/city-picker-dialog";
import { useCities } from "@/features/marketplace/hooks/use-cities";
import { chooseCity } from "@/features/marketplace/utils/choose-city";
import { useCityStore } from "@/stores/city-store";

type CitySwitcherProps = {
  compact?: boolean;
};

export function CitySwitcher({ compact = false }: CitySwitcherProps) {
  const cityId = useCityStore((state) => state.cityId);
  const cities = useCities();
  const [open, setOpen] = useState(false);
  const city = cities.data?.find((item) => item.id === cityId);
  if (!city) return null;

  return (
    <>
      <Button
        variant="text"
        onClick={() => setOpen(true)}
        startIcon={<MapPin className="size-4" aria-hidden="true" />}
        aria-label={`Change city. Currently ${city.name}`}
        sx={{
          minHeight: compact ? 36 : 40,
          borderRadius: "14px",
          px: compact ? 1 : 1.5,
          color: "inherit",
          textTransform: "none",
          fontWeight: 600,
        }}
      >
        {city.name}
      </Button>
      <CityPickerDialog
        open={open}
        cities={cities.data ?? []}
        onDismiss={() => setOpen(false)}
        onSelect={(id) => {
          void chooseCity(id).finally(() => setOpen(false));
        }}
      />
    </>
  );
}

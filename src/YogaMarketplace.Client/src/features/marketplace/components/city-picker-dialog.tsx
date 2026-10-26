import { useState } from "react";
import { Button, Dialog, DialogContent, DialogTitle, List, ListItemButton, ListItemText } from "@mui/material";
import type { CatalogCity } from "@/features/marketplace/types";
import { catalogApi } from "@/features/marketplace/api/catalog-api";

type CityPickerDialogProps = {
  open: boolean;
  cities: CatalogCity[];
  onSelect: (cityId: string) => void;
};

export function CityPickerDialog({ open, cities, onSelect }: CityPickerDialogProps) {
  const [locating, setLocating] = useState(false);

  function useMyLocation() {
    if (locating) return;
    if (!navigator.geolocation) return;
    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (position) => {
        void catalogApi
          .nearestCity(position.coords.latitude, position.coords.longitude)
          .then((city) => {
            if (city) onSelect(city.id);
          })
          .catch(() => {
            // A denied fix, a timeout, or no city with coordinates leaves the list up.
          })
          .finally(() => setLocating(false));
      },
      () => setLocating(false),
    );
  }

  return (
    <Dialog open={open} fullWidth maxWidth="xs" onClose={() => undefined} aria-labelledby="city-picker-title">
      <DialogTitle id="city-picker-title">Choose your city</DialogTitle>
      <DialogContent className="space-y-3">
        <p className="text-sm leading-relaxed text-brand-muted">
          Neighbourhoods and instructors follow the city you pick. You can change it later.
        </p>
        <Button variant="outlined" onClick={useMyLocation} disabled={locating} sx={{ minHeight: 44, borderRadius: "14px" }}>
          {locating ? "Finding your city…" : "Use my location"}
        </Button>
        <List disablePadding>
          {cities.map((city) => (
            <ListItemButton key={city.id} onClick={() => onSelect(city.id)} sx={{ borderRadius: "12px" }}>
              <ListItemText primary={city.name} />
            </ListItemButton>
          ))}
        </List>
      </DialogContent>
    </Dialog>
  );
}

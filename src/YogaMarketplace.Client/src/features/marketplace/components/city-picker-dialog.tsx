import { useState } from "react";
import { Button, Dialog, DialogContent, DialogTitle, List, ListItemButton, ListItemText } from "@mui/material";
import type { CatalogCity } from "@/features/marketplace/types";
import { catalogApi } from "@/features/marketplace/api/catalog-api";

const locationOptions: PositionOptions = { timeout: 10000, maximumAge: 600000 };
const locationMiss = "Couldn't find your city. Pick it from the list.";

type CityPickerDialogProps = {
  open: boolean;
  cities: CatalogCity[];
  onSelect: (cityId: string) => void;
};

export function CityPickerDialog({ open, cities, onSelect }: CityPickerDialogProps) {
  const [locating, setLocating] = useState(false);
  const [locationNote, setLocationNote] = useState<string | null>(null);
  const canLocate = typeof navigator !== "undefined" && Boolean(navigator.geolocation);

  function useMyLocation() {
    if (locating || !navigator.geolocation) return;
    setLocating(true);
    setLocationNote(null);
    navigator.geolocation.getCurrentPosition(
      (position) => {
        void catalogApi
          .nearestCity(position.coords.latitude, position.coords.longitude)
          .then((city) => {
            if (city) onSelect(city.id);
            else setLocationNote(locationMiss);
          })
          .catch(() => setLocationNote(locationMiss))
          .finally(() => setLocating(false));
      },
      () => {
        setLocationNote(locationMiss);
        setLocating(false);
      },
      locationOptions,
    );
  }

  return (
    <Dialog open={open} fullWidth maxWidth="xs" onClose={() => undefined} aria-labelledby="city-picker-title">
      <DialogTitle id="city-picker-title">Choose your city</DialogTitle>
      <DialogContent className="space-y-3">
        <p className="text-sm leading-relaxed text-brand-muted">Neighbourhoods and instructors follow the city you pick.</p>
        {canLocate ? (
          <Button variant="outlined" onClick={useMyLocation} disabled={locating} sx={{ minHeight: 44, borderRadius: "14px" }}>
            {locating ? "Finding your city…" : "Use my location"}
          </Button>
        ) : null}
        {locationNote ? <p className="text-sm leading-relaxed text-brand-muted">{locationNote}</p> : null}
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

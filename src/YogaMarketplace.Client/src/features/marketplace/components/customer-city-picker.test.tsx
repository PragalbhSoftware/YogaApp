import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CityPickerDialog } from "@/features/marketplace/components/city-picker-dialog";
import { CustomerCityPickerBody } from "@/features/marketplace/components/customer-city-picker";
import type { CatalogCity } from "@/features/marketplace/types";
import { catalogApi } from "@/features/marketplace/api/catalog-api";
import { useAreaStore } from "@/stores/area-store";
import { useCityStore } from "@/stores/city-store";

vi.mock("@/features/marketplace/api/catalog-api", () => ({
  catalogApi: { nearestCity: vi.fn(), cities: vi.fn(), areas: vi.fn() },
}));

const cities: CatalogCity[] = [
  { id: "c1", name: "North", stateId: "s1", slug: "n", displayOrder: 1 },
  { id: "c2", name: "South", stateId: "s1", slug: "s", displayOrder: 2 },
];

describe("city picker", () => {
  beforeEach(() => {
    vi.mocked(catalogApi.nearestCity).mockReset();
    useCityStore.getState().clearCity();
    useAreaStore.getState().clearArea();
  });

  it("does not appear before the store has hydrated", () => {
    render(
      <CustomerCityPickerBody
        hydrated={false}
        cityId={null}
        cities={cities}
        citiesLoaded
        onClear={vi.fn()}
        onSelect={vi.fn()}
      />,
    );
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("lists cities once hydration has finished and nothing is stored", () => {
    render(
      <CustomerCityPickerBody
        hydrated
        cityId={null}
        cities={cities}
        citiesLoaded
        onClear={vi.fn()}
        onSelect={vi.fn()}
      />,
    );
    expect(screen.getByRole("dialog", { name: "Choose your city" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "North" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "South" })).toBeInTheDocument();
  });

  it("clears a stale city and the saved neighbourhood, and shows the picker", async () => {
    useCityStore.getState().setCityId("missing");
    useAreaStore.getState().setArea("Old neighbourhood");
    const onClear = vi.fn(() => {
      useCityStore.getState().clearCity();
      useAreaStore.getState().clearArea();
    });

    const view = render(
      <CustomerCityPickerBody
        hydrated
        cityId={useCityStore.getState().cityId}
        cities={cities}
        citiesLoaded
        onClear={onClear}
        onSelect={vi.fn()}
      />,
    );

    await waitFor(() => expect(onClear).toHaveBeenCalled());
    expect(useCityStore.getState().cityId).toBeNull();
    expect(useAreaStore.getState().areaName).toBeNull();
    view.rerender(
      <CustomerCityPickerBody
        hydrated
        cityId={null}
        cities={cities}
        citiesLoaded
        onClear={onClear}
        onSelect={vi.fn()}
      />,
    );
    expect(screen.getByRole("dialog", { name: "Choose your city" })).toBeInTheDocument();
  });
});

describe("Use my location", () => {
  beforeEach(() => {
    vi.mocked(catalogApi.nearestCity).mockReset();
  });

  it("selects the nearest city when location is allowed", async () => {
    const onSelect = vi.fn();
    vi.mocked(catalogApi.nearestCity).mockResolvedValue(cities[1]);
    vi.stubGlobal("navigator", {
      geolocation: {
        getCurrentPosition: (success: PositionCallback) =>
          success({ coords: { latitude: 1, longitude: 2 } } as GeolocationPosition),
      },
    });

    render(<CityPickerDialog open cities={cities} onSelect={onSelect} />);
    fireEvent.click(screen.getByRole("button", { name: "Use my location" }));

    await waitFor(() => expect(onSelect).toHaveBeenCalledWith("c2"));
    expect(catalogApi.nearestCity).toHaveBeenCalledWith(1, 2);
    vi.unstubAllGlobals();
  });

  it("keeps the list when location is denied", async () => {
    const onSelect = vi.fn();
    vi.stubGlobal("navigator", {
      geolocation: {
        getCurrentPosition: (_success: PositionCallback, error: PositionErrorCallback) =>
          error({ code: 1, message: "denied" } as GeolocationPositionError),
      },
    });

    render(<CityPickerDialog open cities={cities} onSelect={onSelect} />);
    fireEvent.click(screen.getByRole("button", { name: "Use my location" }));

    await waitFor(() => expect(screen.getByRole("button", { name: "North" })).toBeInTheDocument());
    expect(onSelect).not.toHaveBeenCalled();
    expect(catalogApi.nearestCity).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });
});

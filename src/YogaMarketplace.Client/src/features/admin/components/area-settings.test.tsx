import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AreaSettings } from "@/features/admin/components/area-settings";

const { areas, activeStates, activeCities } = vi.hoisted(() => ({
  areas: vi.fn(),
  activeStates: vi.fn(),
  activeCities: vi.fn(),
}));

vi.mock("@/features/admin/api/admin-api", () => ({
  adminApi: {
    areas,
    activeStates,
    activeCities,
    createArea: vi.fn(),
    updateArea: vi.fn(),
  },
}));

function renderAreas() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <AreaSettings />
    </QueryClientProvider>,
  );
}

describe("Areas form", () => {
  beforeEach(() => {
    areas.mockReset().mockResolvedValue([]);
    activeStates.mockReset().mockResolvedValue([{ id: "s1", name: "Region Alpha", code: "RA", displayOrder: 0 }]);
    activeCities.mockReset().mockResolvedValue([{ id: "c1", name: "Harbor", stateId: "s1", slug: "harbor", displayOrder: 0 }]);
  });

  it("requires a state and keeps city cleared until one is chosen", async () => {
    renderAreas();
    const city = await screen.findByRole("combobox", { name: "City" });
    expect(city).toHaveAttribute("aria-disabled", "true");
    expect(activeCities).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Add neighbourhood" }));
    expect(await screen.findByText("Pick a state.")).toBeInTheDocument();
    expect(screen.getByText("Pick a city.")).toBeInTheDocument();

    fireEvent.mouseDown(screen.getByRole("combobox", { name: "State" }));
    fireEvent.click(await screen.findByRole("option", { name: "Region Alpha" }));

    await waitFor(() => expect(screen.getByRole("combobox", { name: "City" })).not.toHaveAttribute("aria-disabled", "true"));
    expect(activeCities).toHaveBeenCalledWith("s1");
    expect(screen.getByRole("combobox", { name: "City" })).not.toHaveTextContent("Harbor");
  });
});

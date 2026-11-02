import { render, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { HomePage } from "@/features/marketplace/pages/home-page";
import { useAreaStore } from "@/stores/area-store";
import { useCityStore } from "@/stores/city-store";

const useInstructors = vi.hoisted(() =>
  vi.fn(() => ({
    data: [],
    isLoading: false,
    isError: false,
    error: null,
    isFetching: false,
    isPlaceholderData: false,
    refetch: vi.fn(),
  })),
);

vi.mock("@/features/marketplace/hooks/use-cities", () => ({
  useCities: () => ({
    data: [{ id: "c1", name: "Harbor", stateId: "s1", slug: "h", displayOrder: 1 }],
    isSuccess: true,
    isLoading: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
  }),
}));

vi.mock("@/features/marketplace/hooks/use-areas", () => ({
  useAreas: () => ({
    data: [{ id: "a1", cityId: "c1", cityName: "Harbor", stateId: "s1", stateName: "West", pincode: "400050", name: "Bandra" }],
    isSuccess: true,
    isLoading: false,
    isError: false,
  }),
}));

vi.mock("@/features/marketplace/hooks/use-instructors", () => ({
  useInstructors,
}));

vi.mock("@/features/marketplace/hooks/use-site", () => ({
  useSiteBanner: () => ({ data: undefined, isLoading: false }),
}));

vi.mock("@/features/marketplace/hooks/use-closed-area-reset", () => ({
  useClosedAreaReset: () => undefined,
}));

describe("home lists with city only", () => {
  beforeEach(() => {
    useInstructors.mockClear();
    useCityStore.getState().setCityId("c1");
    useAreaStore.getState().clearArea();
  });

  it("browses with areaId null when no neighbourhood is saved", async () => {
    render(
      <MemoryRouter>
        <HomePage />
      </MemoryRouter>,
    );
    await waitFor(() => expect(useInstructors).toHaveBeenCalled());
    expect(useInstructors).toHaveBeenCalledWith("c1", null, "");
  });
});

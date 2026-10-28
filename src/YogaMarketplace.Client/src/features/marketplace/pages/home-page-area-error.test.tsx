import { render, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { HomePage } from "@/features/marketplace/pages/home-page";
import { ApiError } from "@/services/http/api-error";
import { useAreaStore } from "@/stores/area-store";
import { useCityStore } from "@/stores/city-store";

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
  useInstructors: () => ({
    data: undefined,
    isLoading: false,
    isError: true,
    error: new ApiError("Pick a neighbourhood in that city.", 400),
    isFetching: false,
    isPlaceholderData: false,
    refetch: vi.fn(),
  }),
}));

vi.mock("@/features/marketplace/hooks/use-site", () => ({
  useSiteBanner: () => ({ data: undefined, isLoading: false }),
}));

vi.mock("@/features/marketplace/hooks/use-closed-area-reset", () => ({
  useClosedAreaReset: () => undefined,
}));

describe("home clears a bad neighbourhood", () => {
  beforeEach(() => {
    useCityStore.getState().setCityId("c1");
    useAreaStore.getState().setArea("Bandra");
  });

  it("clears only the area store when browse returns a neighbourhood 400", async () => {
    render(
      <MemoryRouter>
        <HomePage />
      </MemoryRouter>,
    );
    await waitFor(() => expect(useAreaStore.getState().areaName).toBeNull());
    expect(useCityStore.getState().cityId).toBe("c1");
  });
});

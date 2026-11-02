import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { HomePage } from "@/features/marketplace/pages/home-page";

const cities = vi.hoisted(() => ({
  current: {
    data: [] as { id: string; name: string }[],
    isSuccess: true,
    isLoading: false,
    isError: false,
    error: null as unknown,
    refetch: vi.fn(),
  },
}));

vi.mock("@/features/marketplace/hooks/use-cities", () => ({
  useCities: () => cities.current,
}));

vi.mock("@/features/marketplace/hooks/use-instructors", () => ({
  useInstructors: () => ({ data: [], isLoading: false, isError: false, error: null, refetch: vi.fn() }),
}));

vi.mock("@/features/marketplace/hooks/use-areas", () => ({
  useAreas: () => ({ data: [], isSuccess: true, isLoading: false, isError: false }),
}));

vi.mock("@/features/marketplace/hooks/use-site", () => ({
  useSiteBanner: () => ({ data: undefined, isLoading: false }),
}));

vi.mock("@/features/marketplace/hooks/use-closed-area-reset", () => ({
  useClosedAreaReset: () => undefined,
}));

describe("home when the city list is not usable", () => {
  beforeEach(() => {
    cities.current = {
      data: [],
      isSuccess: true,
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    };
  });

  it("shows an empty state when no city is open", async () => {
    render(
      <MemoryRouter>
        <HomePage />
      </MemoryRouter>,
    );
    expect(await screen.findByText("We're not live in any city yet")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("shows an error with retry when the city list fails", async () => {
    cities.current = {
      ...cities.current,
      data: [],
      isSuccess: false,
      isError: true,
      error: new Error("offline"),
    };
    render(
      <MemoryRouter>
        <HomePage />
      </MemoryRouter>,
    );
    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Try again" })).toBeInTheDocument();
  });
});

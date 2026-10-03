import type { ReactNode } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useClosedAreaReset } from "@/features/marketplace/hooks/use-closed-area-reset";
import { useAreaStore } from "@/stores/area-store";

const { areas, toastMessage } = vi.hoisted(() => ({ areas: vi.fn(), toastMessage: vi.fn() }));

vi.mock("@/features/marketplace/api/catalog-api", () => ({ catalogApi: { areas } }));
vi.mock("sonner", () => ({ toast: { message: toastMessage } }));

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

function renderWithSaved(city: string, areaName: string) {
  useAreaStore.getState().setArea(city, areaName);
  return renderHook(() => useClosedAreaReset(city, areaName), { wrapper });
}

describe("useClosedAreaReset", () => {
  beforeEach(() => {
    areas.mockReset();
    toastMessage.mockReset();
    useAreaStore.getState().clearArea();
  });

  it("clears a saved area that is no longer open and asks to pick again", async () => {
    areas.mockResolvedValue([{ id: "a1", city: "Pune", name: "Baner" }]);
    renderWithSaved("Pune", "Kothrud");

    await waitFor(() => expect(useAreaStore.getState().areaName).toBeNull());
    expect(useAreaStore.getState().city).toBeNull();
    expect(toastMessage).toHaveBeenCalledWith(expect.stringContaining("Kothrud"));
  });

  it("keeps a saved area that is still open, ignoring letter case", async () => {
    areas.mockResolvedValue([{ id: "a1", city: "Pune", name: "Kothrud" }]);
    renderWithSaved("pune", "kothrud");

    await waitFor(() => expect(areas).toHaveBeenCalled());
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(useAreaStore.getState().areaName).toBe("kothrud");
    expect(toastMessage).not.toHaveBeenCalled();
  });

  it("keeps the saved area when the list fails to load", async () => {
    areas.mockRejectedValue(new Error("offline"));
    renderWithSaved("Pune", "Kothrud");

    await waitFor(() => expect(areas).toHaveBeenCalled());
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(useAreaStore.getState().areaName).toBe("Kothrud");
  });
});

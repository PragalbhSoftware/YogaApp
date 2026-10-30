import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { instructorsApi } from "@/features/marketplace/api/instructors-api";
import { useInstructors } from "@/features/marketplace/hooks/use-instructors";

vi.mock("@/features/marketplace/api/instructors-api", () => ({
  instructorsApi: { browse: vi.fn() },
}));

function wrap(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe("useInstructors", () => {
  beforeEach(() => {
    vi.mocked(instructorsApi.browse).mockReset();
    vi.mocked(instructorsApi.browse).mockResolvedValue([]);
  });

  it("does not fetch without a city", () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useInstructors(null, "a1", ""), { wrapper: wrap(client) });
    expect(instructorsApi.browse).not.toHaveBeenCalled();
  });

  it("changes the query key when the city changes", async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { rerender } = renderHook(
      ({ cityId }: { cityId: string }) => useInstructors(cityId, null, ""),
      { wrapper: wrap(client), initialProps: { cityId: "c1" } },
    );
    await waitFor(() => expect(instructorsApi.browse).toHaveBeenCalledWith("c1", null, "", 1));
    rerender({ cityId: "c2" });
    await waitFor(() => expect(instructorsApi.browse).toHaveBeenCalledWith("c2", null, "", 1));
  });

  it("keeps placeholder data only when the page changes", async () => {
    const first = [{ id: "p1" }];
    const second = [{ id: "p2" }];
    vi.mocked(instructorsApi.browse)
      .mockResolvedValueOnce(first as never)
      .mockResolvedValueOnce(second as never)
      .mockResolvedValue([] as never);

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result, rerender } = renderHook(
      ({ page, mode }: { page: number; mode: "" | "Online" }) => useInstructors("c1", null, mode, page),
      { wrapper: wrap(client), initialProps: { page: 1, mode: "" as "" | "Online" } },
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(first);

    rerender({ page: 2, mode: "" });
    expect(result.current.isPlaceholderData).toBe(true);
    expect(result.current.data).toEqual(first);
    await waitFor(() => expect(result.current.isPlaceholderData).toBe(false));
    expect(result.current.data).toEqual(second);

    rerender({ page: 2, mode: "Online" });
    expect(result.current.isPlaceholderData).toBe(false);
  });
});

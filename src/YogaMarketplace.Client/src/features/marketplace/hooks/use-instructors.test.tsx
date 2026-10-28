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
});

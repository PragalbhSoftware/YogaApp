import type { ReactNode } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, renderHook, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { PurchaseStatus } from "@/features/credits/components/purchase-status";
import { useBuyPack } from "@/features/credits/hooks/use-buy-pack";
import { packOrderPollMs, usePackOrderStatus } from "@/features/credits/hooks/use-credits";
import { readPendingPackOrder, savePendingPackOrder } from "@/features/credits/utils/pending-pack-order";
import { useAuthStore } from "@/stores/auth-store";

const { orderStatus } = vi.hoisted(() => ({ orderStatus: vi.fn() }));

vi.mock("@/features/credits/api/credits-api", () => ({ creditsApi: { orderStatus } }));
vi.mock("@/features/credits/utils/purchase-stage", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/features/credits/utils/purchase-stage")>()),
  packConfirmTimeoutMs: 50,
}));

const userId = "user-1";

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe("useBuyPack", () => {
  beforeEach(() => {
    orderStatus.mockReset();
    sessionStorage.clear();
    useAuthStore.setState({ token: "token", user: { id: userId, name: "Kavya", phone: "+919200000000", gender: null, role: "Customer" } });
  });

  afterEach(() => {
    useAuthStore.setState({ token: null, user: null });
  });

  it("resumes confirming a paid order after a refresh and clears it once credits exist", async () => {
    savePendingPackOrder("order_paid", userId);
    orderStatus.mockResolvedValue({ orderId: "order_paid", status: "Completed", batch: null });

    const { result } = renderHook(() => useBuyPack(), { wrapper });

    expect(result.current.stage).toBe("confirming");
    expect(result.current.busy).toBe(true);
    await waitFor(() => expect(result.current.stage).toBe("done"));
    expect(orderStatus).toHaveBeenCalledWith("order_paid");
    expect(readPendingPackOrder(userId)).toBeNull();
  });

  it("does not resume another user's order", () => {
    savePendingPackOrder("order_other", "user-2");

    const { result } = renderHook(() => useBuyPack(), { wrapper });

    expect(result.current.stage).toBe("idle");
    expect(orderStatus).not.toHaveBeenCalled();
  });

  it("stops confirming after the time limit, keeps the order for a later refresh, and stays busy", async () => {
    savePendingPackOrder("order_slow", userId);
    orderStatus.mockResolvedValue({ orderId: "order_slow", status: "Open", batch: null });

    const { result } = renderHook(() => useBuyPack(), { wrapper });

    await waitFor(() => expect(result.current.stage).toBe("slow"));
    expect(result.current.busy).toBe(true);
    expect(readPendingPackOrder(userId)).toBe("order_slow");
  });
});

describe("usePackOrderStatus", () => {
  beforeEach(() => orderStatus.mockReset());

  it("keeps polling an order marked Failed by an earlier attempt until the capture lands", async () => {
    orderStatus
      .mockResolvedValueOnce({ orderId: "order_retried", status: "Failed", batch: null })
      .mockResolvedValue({ orderId: "order_retried", status: "Completed", batch: null });

    const { result } = renderHook(() => usePackOrderStatus("order_retried"), { wrapper });

    await waitFor(() => expect(result.current.data?.status).toBe("Completed"), { timeout: packOrderPollMs * 2 });
    expect(orderStatus).toHaveBeenCalledTimes(2);
  });
});

describe("PurchaseStatus", () => {
  it("tells the customer not to pay again when confirming takes too long", () => {
    render(<PurchaseStatus stage="slow" batch={null} onFindInstructor={vi.fn()} onDismiss={vi.fn()} />);

    expect(screen.getByRole("heading", { name: "Still confirming" })).toBeInTheDocument();
    expect(screen.getByText("Don’t pay again. Check your credits in a few minutes.")).toBeInTheDocument();
  });
});

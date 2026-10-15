import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { PacksPage } from "@/features/credits/pages/packs-page";

vi.mock("@/features/auth/hooks/use-auth", () => ({ useAuth: () => ({ isAuthenticated: false }) }));
vi.mock("@/features/credits/hooks/use-credits", () => ({
  usePacks: () => ({ isLoading: false, isError: false, isSuccess: true, data: [{ id: "pack-1" }] }),
}));
vi.mock("@/features/credits/hooks/use-buy-pack", () => ({
  useBuyPack: () => ({ stage: "idle", batch: null, busy: false, buyingId: null }),
}));
vi.mock("@/features/credits/components/pack-card", () => ({ PackCard: () => null }));

describe("PacksPage", () => {
  it("shows the pay agreement with same-tab links to Terms and Refund policy", () => {
    render(
      <MemoryRouter>
        <PacksPage />
      </MemoryRouter>,
    );

    expect(screen.getByText(/By paying you agree to the/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Terms" })).toHaveAttribute("href", "/terms");
    expect(screen.getByRole("link", { name: "Refund policy" })).not.toHaveAttribute("target");
  });
});

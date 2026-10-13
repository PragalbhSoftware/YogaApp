import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { authApi } from "@/features/auth/api/auth-api";
import { ConsentPrompt } from "@/features/auth/components/consent-prompt";
import { useAuthStore } from "@/stores/auth-store";

const user = { id: "u1", name: "Test", phone: "+919000000000", gender: null, role: "Customer" };

function renderPrompt() {
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter>
        <ConsentPrompt />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("ConsentPrompt", () => {
  beforeEach(() => useAuthStore.getState().signOut());
  afterEach(() => vi.restoreAllMocks());

  it("stays hidden when the user is up to date", () => {
    useAuthStore.getState().signIn("token", { ...user, consentRequired: false });
    renderPrompt();
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("asks once after a policy bump and closes when accepted", async () => {
    const accept = vi.spyOn(authApi, "acceptConsent").mockResolvedValue({ ...user, consentRequired: false });
    useAuthStore.getState().signIn("token", { ...user, consentRequired: true });
    renderPrompt();

    expect(screen.getByRole("link", { name: "Terms of use" })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "I accept" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(accept).toHaveBeenCalledTimes(1);
    expect(useAuthStore.getState().user?.consentRequired).toBe(false);
  });
});

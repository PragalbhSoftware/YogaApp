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

  it("stays hidden when the user is up to date", async () => {
    const me = vi.spyOn(authApi, "me").mockResolvedValue({ ...user, consentRequired: false });
    useAuthStore.getState().signIn("token", { ...user, consentRequired: false });
    renderPrompt();
    await waitFor(() => expect(me).toHaveBeenCalledTimes(1));
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("opens on load when the server says a stored session needs the new policies", async () => {
    vi.spyOn(authApi, "me").mockResolvedValue({ ...user, name: "Renamed", consentRequired: true });
    useAuthStore.getState().signIn("token", { ...user, consentRequired: false });
    renderPrompt();

    expect(await screen.findByRole("dialog")).toBeTruthy();
    expect(useAuthStore.getState().user?.name).toBe("Renamed");
    expect(useAuthStore.getState().token).toBe("token");
  });

  it("does not call the API without a session", () => {
    const me = vi.spyOn(authApi, "me");
    renderPrompt();
    expect(me).not.toHaveBeenCalled();
  });

  it("asks once after a policy bump and closes when accepted", async () => {
    const me = vi.spyOn(authApi, "me").mockResolvedValue({ ...user, consentRequired: true });
    const accept = vi.spyOn(authApi, "acceptConsent").mockResolvedValue({ ...user, consentRequired: false });
    useAuthStore.getState().signIn("token", { ...user, consentRequired: true });
    renderPrompt();
    await waitFor(() => expect(me).toHaveBeenCalledTimes(1));

    expect(screen.getByRole("link", { name: "Terms of use" })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "I accept" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(accept).toHaveBeenCalledTimes(1);
    expect(useAuthStore.getState().user?.consentRequired).toBe(false);
  });
});

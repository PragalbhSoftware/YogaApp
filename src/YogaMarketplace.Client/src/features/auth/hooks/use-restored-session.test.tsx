import { renderHook, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SESSION_RESTORE_TIMEOUT_MS, useRestoredSession } from "@/features/auth/hooks/use-restored-session";
import { refreshSession } from "@/services/http/http-client";
import { useAuthStore } from "@/stores/auth-store";

vi.mock("@/services/http/http-client", () => ({ refreshSession: vi.fn() }));

const customer = { id: "u1", name: "Asha", phone: "+919000000000", gender: null, role: "Customer" };

function storedText() {
  return [localStorage, sessionStorage]
    .flatMap((storage) => Object.keys(storage).map((key) => `${key}=${storage.getItem(key)}`))
    .join("\n");
}

describe("session storage", () => {
  afterEach(() => {
    useAuthStore.getState().signOut();
    sessionStorage.clear();
    vi.mocked(refreshSession).mockReset();
  });

  it("keeps the token in memory only after sign-in", () => {
    useAuthStore.getState().signIn("secret-access-token", customer);

    expect(useAuthStore.getState().token).toBe("secret-access-token");
    expect(storedText()).not.toContain("secret-access-token");
    expect(localStorage.getItem("ym.auth")).toBeNull();
  });

  it("restores the session from the refresh cookie on load and drops a session an older build stored", async () => {
    localStorage.setItem("ym.auth", JSON.stringify({ state: { token: "old-stored-token" } }));
    vi.mocked(refreshSession).mockImplementation(() => {
      useAuthStore.getState().signIn("restored-token", customer);
      return Promise.resolve({ ok: true, token: "restored-token" });
    });

    const { result } = renderHook(() => useRestoredSession());

    expect(result.current).toBe(false);
    await waitFor(() => expect(result.current).toBe(true));
    expect(refreshSession).toHaveBeenCalledWith(SESSION_RESTORE_TIMEOUT_MS);
    expect(useAuthStore.getState().user?.id).toBe("u1");
    expect(storedText()).not.toContain("old-stored-token");
    expect(storedText()).not.toContain("restored-token");
  });

  it("finishes restoring as a signed-out visitor when there is no refresh cookie", async () => {
    vi.mocked(refreshSession).mockResolvedValue({ ok: false, error: new Error("401") });

    const { result } = renderHook(() => useRestoredSession());

    await waitFor(() => expect(result.current).toBe(true));
    expect(useAuthStore.getState().token).toBeNull();
  });
});

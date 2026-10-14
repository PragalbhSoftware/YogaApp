import { renderHook, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SESSION_RESTORE_TIMEOUT_MS, useRestoredSession } from "@/features/auth/hooks/use-restored-session";
import { ApiError } from "@/services/http/api-error";
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
    localStorage.clear();
    sessionStorage.clear();
    vi.mocked(refreshSession).mockReset();
  });

  it("keeps the token in memory only after sign-in", () => {
    useAuthStore.getState().signIn("secret-access-token", customer);

    expect(useAuthStore.getState().token).toBe("secret-access-token");
    expect(storedText()).not.toContain("secret-access-token");
    expect(localStorage.getItem("ym.auth")).toBeNull();
  });

  it("sets only the non-secret signed-in flag on sign-in and clears it on sign-out", () => {
    useAuthStore.getState().signIn("secret-access-token", customer);
    expect(storedText()).toBe("ym.signedIn=1");

    useAuthStore.getState().signOut();
    expect(storedText()).toBe("");
  });

  it("restores the session from the refresh cookie on load and drops a session an older build stored", async () => {
    localStorage.setItem("ym.signedIn", "1");
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

  it("skips the refresh call for a guest who never signed in", () => {
    const { result } = renderHook(() => useRestoredSession());

    expect(result.current).toBe(true);
    expect(refreshSession).not.toHaveBeenCalled();
    expect(useAuthStore.getState().token).toBeNull();
  });

  it("finishes as a signed-out visitor and clears the flag when the refresh cookie is refused", async () => {
    localStorage.setItem("ym.signedIn", "1");
    vi.mocked(refreshSession).mockResolvedValue({ ok: false, error: new ApiError("Session expired.", 401) });

    const { result } = renderHook(() => useRestoredSession());

    await waitFor(() => expect(result.current).toBe(true));
    expect(useAuthStore.getState().token).toBeNull();
    expect(localStorage.getItem("ym.signedIn")).toBeNull();
  });

  it("keeps the flag when the server is unreachable, so the next load tries again", async () => {
    localStorage.setItem("ym.signedIn", "1");
    vi.mocked(refreshSession).mockResolvedValue({ ok: false, error: new ApiError("Can't reach the server.", 0, true) });

    const { result } = renderHook(() => useRestoredSession());

    await waitFor(() => expect(result.current).toBe(true));
    expect(localStorage.getItem("ym.signedIn")).toBe("1");
  });
});

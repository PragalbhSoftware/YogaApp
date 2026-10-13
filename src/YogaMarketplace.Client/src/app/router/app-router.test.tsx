import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AppRouter } from "@/app/router/app-router";
import { routes } from "@/constants/routes";
import { authApi } from "@/features/auth/api/auth-api";
import { useAuthStore } from "@/stores/auth-store";

describe("policy pages", () => {
  beforeEach(() => {
    useAuthStore.getState().signOut();
    vi.spyOn(authApi, "me").mockImplementation(() => new Promise(() => {}));
  });
  afterEach(() => vi.restoreAllMocks());

  it.each([
    [null, routes.terms],
    ["Customer", routes.privacy],
    ["Provider", routes.terms],
    ["Provider", routes.privacy],
    ["Admin", routes.refundPolicy],
  ])("are open to %s at %s without a redirect", async (role, path) => {
    if (role) {
      useAuthStore.getState().signIn("token", { id: "u1", name: "Test", phone: "+919000000000", gender: null, role });
    }
    window.history.pushState({}, "", path);

    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <AppRouter />
      </QueryClientProvider>,
    );

    expect(await screen.findByText("Yoga Marketplace · India")).toBeInTheDocument();
    expect(window.location.pathname).toBe(path);
  });
});
